using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Router;

class RouterImpl : IRouter
{
    public RouterImpl(string name, Interface[] interfaces, IModule[] modules, IRoutingProtocol routingProtocol, InterfaceChains interfaceChains)
    {
        this.name = name;
        this.modules = modules;
        this.routingProtocol = routingProtocol;
        this.interfaceChains = interfaceChains;
        this.interfaces = interfaces.ToDictionary(x => x.Name, x => x);
    }

    public async Task Start()
    {
        var rootContext = new RootContext(interfaceChains);

        await routingProtocol.Start(new RouterMetadata(name, interfaces.Keys.ToList())).ConfigureAwait(false);
        await Task.WhenAll(interfaces.Values.Select(p => p.Initialize(interfaceChains, rootContext))).ConfigureAwait(false);

        //Start modules in order
        foreach (var module in modules)
        {
            log.Debug($"Starting module {module}");
            await module.Start(rootContext).ConfigureAwait(false);
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
    
    string name;
    IModule[] modules;
    IRoutingProtocol routingProtocol;
    InterfaceChains interfaceChains;
    Dictionary<string, Interface> interfaces;
    static ILog log = LogManager.GetLogger<RouterImpl>();
}