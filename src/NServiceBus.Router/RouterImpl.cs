using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Router;

class RouterImpl : IRouter
{
    public RouterImpl(string name, Interface[] interfaces, SendOnlyInterface[] sendOnlyInterfaces, IModule[] modules, IRoutingProtocol routingProtocol, InterfaceChains interfaceChains, SettingsHolder extensibilitySettings)
    {
        this.name = name;
        this.sendOnlyInterfaces = sendOnlyInterfaces;
        this.modules = modules;
        this.routingProtocol = routingProtocol;
        this.interfaceChains = interfaceChains;
        this.extensibilitySettings = extensibilitySettings;
        this.interfaces = interfaces;
        stopTokenSource = new CancellationTokenSource();
    }

    public async Task Initialize()
    {
        if (initialized)
        {
            throw new Exception("The router has already been initialized.");
        }

        rootContext = new RootContext(interfaceChains, name, stopTokenSource.Token);
        await routingProtocol.Start(new RouterMetadata(name, interfaces.Select(i => i.Name).ToList())).ConfigureAwait(false);

        foreach (var iface in sendOnlyInterfaces)
        {
            await iface.Initialize(interfaceChains).ConfigureAwait(false);
        }

        foreach (var iface in interfaces)
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

        await Task.WhenAll(interfaces.Select(p => p.StartReceiving())).ConfigureAwait(false);
    }

    public async Task Stop()
    {
        stopTokenSource.Cancel();
        await Task.WhenAll(interfaces.Select(s => s.StopReceiving())).ConfigureAwait(false);

        //Stop modules in reverse order
        foreach (var module in modules.Reverse())
        {
            log.Debug($"Stopping module {module}");
            await module.Stop().ConfigureAwait(false);
            log.Debug($"Stopped module {module}");
        }

        await Task.WhenAll(interfaces.Select(s => s.Stop())).ConfigureAwait(false);
        await Task.WhenAll(sendOnlyInterfaces.Select(s => s.Stop())).ConfigureAwait(false);
        await routingProtocol.Stop().ConfigureAwait(false);
    }

    bool initialized;
    string name;
    Interface[] interfaces;
    SendOnlyInterface[] sendOnlyInterfaces;
    IModule[] modules;
    IRoutingProtocol routingProtocol;
    InterfaceChains interfaceChains;
    SettingsHolder extensibilitySettings;
    static ILog log = LogManager.GetLogger<RouterImpl>();
    RootContext rootContext;
    readonly CancellationTokenSource stopTokenSource;
}