using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class RouterImpl : IRouter
{
    public RouterImpl(string name, Interface[] interfaces, IRoutingProtocol routingProtocol, InterfaceChains interfaceChains)
    {
        this.name = name;
        this.routingProtocol = routingProtocol;
        this.interfaceChains = interfaceChains;
        this.interfaces = interfaces.ToDictionary(x => x.Name, x => x);
    }

    public async Task Start()
    {
        await routingProtocol.Start(new RouterMetadata(name, interfaces.Keys.ToList())).ConfigureAwait(false);
        await Task.WhenAll(interfaces.Values.Select(p => p.Initialize(interfaceChains))).ConfigureAwait(false);
        await Task.WhenAll(interfaces.Values.Select(p => p.StartReceiving())).ConfigureAwait(false);
    }

    public async Task Stop()
    {
        await Task.WhenAll(interfaces.Values.Select(s => s.StopReceiving())).ConfigureAwait(false);
        await Task.WhenAll(interfaces.Values.Select(s => s.Stop())).ConfigureAwait(false);
        await routingProtocol.Stop().ConfigureAwait(false);
    }
    
    string name;
    IRoutingProtocol routingProtocol;
    InterfaceChains interfaceChains;
    Dictionary<string, Interface> interfaces;
}