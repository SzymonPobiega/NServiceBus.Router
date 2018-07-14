using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class SubscribePreroutingTerminator : ChainTerminator<SubscribePreroutingContext>
{
    public SubscribePreroutingTerminator(IRoutingProtocol routingProtocol)
    {
        this.routingProtocol = routingProtocol;
    }
    protected override Task Terminate(SubscribePreroutingContext context)
    {
        var outgoingInterfaces = routingProtocol.RouteTable.GetOutgoingInterfaces(context.IncomingInterface, context.Destinations);
        var routes = routingProtocol.RouteTable.Route(context.IncomingInterface, context.Destinations);

        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var forkTasks = outgoingInterfaces
            .Select(iface =>
            {
                var chains = interfaces.GetChainsFor(iface);
                var chain = chains.Get<ForwardSubscribeContext>();
                return chain.Invoke(new ForwardSubscribeContext(iface, routes.ToArray(), context));
            });

        return Task.WhenAll(forkTasks);
    }

    IRoutingProtocol routingProtocol;
}