using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class SubscribePreroutingTerminator : ChainTerminator<SubscribePreroutingContext>
{
    public SubscribePreroutingTerminator(IRoutingProtocol routingProtocol, RuntimeTypeGenerator typeGenerator)
    {
        this.routingProtocol = routingProtocol;
        this.typeGenerator = typeGenerator;
    }
    protected override async Task<bool> Terminate(SubscribePreroutingContext context)
    {
        if (!context.Destinations.Any())
        {
            return false;
        }

        var outgoingInterfaces = routingProtocol.RouteTable.GetOutgoingInterfaces(context.IncomingInterface, context.Destinations);
        var routes = routingProtocol.RouteTable.Route(context.IncomingInterface, context.Destinations).ToArray();

        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var forkTasks = outgoingInterfaces
            .Select(iface =>
            {
                var chains = interfaces.GetChainsFor(iface);
                var chain = chains.Get<ForwardSubscribeContext>();
                return chain.Invoke(new ForwardSubscribeContext(iface, routes, typeGenerator.GetType(context.MessageType), context.SubscriberEndpoint, context.SubscriberAddress, context));
            });

        await Task.WhenAll(forkTasks).ConfigureAwait(false);

        return true;
    }

    IRoutingProtocol routingProtocol;
    RuntimeTypeGenerator typeGenerator;
}