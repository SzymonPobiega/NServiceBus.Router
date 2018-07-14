using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class UnsubscribePreroutingTerminator : ChainTerminator<UnsubscribePreroutingContext>
{
    public UnsubscribePreroutingTerminator(IRoutingProtocol routingProtocol)
    {
        this.routingProtocol = routingProtocol;
    }
    protected override Task Terminate(UnsubscribePreroutingContext context)
    {
        var outgoingInterfaces = routingProtocol.RouteTable.GetOutgoingInterfaces(context.IncomingInterface, context.Destinations);
        var routes = routingProtocol.RouteTable.Route(context.IncomingInterface, context.Destinations);

        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var forkTasks = outgoingInterfaces
            .Select(iface =>
            {
                var chains = interfaces.GetChainsFor(iface);
                var chain = chains.Get<ForwardUnsubscribeContext>();
                return chain.Invoke(new ForwardUnsubscribeContext(iface, routes.ToArray(), context));
            });

        return Task.WhenAll(forkTasks);
    }

    IRoutingProtocol routingProtocol;
}