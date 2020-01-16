using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Router;

class RouterImpl : IRouter
{
    public RouterImpl(string name, Interface[] interfaces, IModule[] modules, IRoutingProtocol routingProtocol, InterfaceChains interfaceChains, SettingsHolder extensibilitySettings)
    {
        this.name = name;
        this.modules = modules;
        this.routingProtocol = routingProtocol;
        this.interfaceChains = interfaceChains;
        this.extensibilitySettings = extensibilitySettings;
        this.interfaces = interfaces.ToDictionary(x => x.Name, x => x);
    }

    public async Task Initialize()
    {
        if (initialized)
        {
            throw new Exception("The router has already been initialized.");
        }

        rootContext = new RootContext(interfaceChains, name);
        await routingProtocol.Start(new RouterMetadata(name, interfaces.Keys.ToList())).ConfigureAwait(false);

        foreach (var iface in interfaces.Values)
        {
            await iface.Initialize(interfaceChains, rootContext).ConfigureAwait(false);
        }

        initialized = true;
    }

    public async Task Start()
    {
        if (!initialized)
        {
            await Initialize().ConfigureAwait(false);
        }

        //Start modules in order
        foreach (var module in modules)
        {
            log.Debug($"Starting module {module}");
            await module.Start(rootContext, extensibilitySettings).ConfigureAwait(false);
            log.Debug($"Started module {module}");
        }

        await Task.WhenAll(interfaces.Values.Select(p => p.StartReceiving())).ConfigureAwait(false);
    }

    public async Task Stop()
    {
        await Task.WhenAll(interfaces.Values.Select(s => s.StopReceiving())).ConfigureAwait(false);

        //Stop modules in reverse order
        foreach (var module in modules.Reverse())
        {
            log.Debug($"Stopping module {module}");
            await module.Stop().ConfigureAwait(false);
            log.Debug($"Stopped module {module}");
        }

        await Task.WhenAll(interfaces.Values.Select(s => s.Stop())).ConfigureAwait(false);
        await routingProtocol.Stop().ConfigureAwait(false);
    }

    bool initialized;
    string name;
    IModule[] modules;
    IRoutingProtocol routingProtocol;
    InterfaceChains interfaceChains;
    SettingsHolder extensibilitySettings;
    Dictionary<string, Interface> interfaces;
    static ILog log = LogManager.GetLogger<RouterImpl>();
    RootContext rootContext;
}