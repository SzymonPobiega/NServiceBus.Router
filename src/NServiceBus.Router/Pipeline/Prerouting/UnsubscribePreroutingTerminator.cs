using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class UnsubscribePreroutingTerminator : ChainTerminator<UnsubscribePreroutingContext>
{
    public UnsubscribePreroutingTerminator(IRoutingProtocol routingProtocol, RuntimeTypeGenerator typeGenerator)
    {
        this.routingProtocol = routingProtocol;
        this.typeGenerator = typeGenerator;
    }
    protected override Task Terminate(UnsubscribePreroutingContext context)
    {
        if (!context.Destinations.Any())
        {
            throw new UnforwardableMessageException("No destinations could be found for message.");
        }

        var outgoingInterfaces = routingProtocol.RouteTable.GetOutgoingInterfaces(context.IncomingInterface, context.Destinations);
        var routes = routingProtocol.RouteTable.Route(context.IncomingInterface, context.Destinations);

        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var forkTasks = outgoingInterfaces
            .Select(iface =>
            {
                var chains = interfaces.GetChainsFor(iface);
                var chain = chains.Get<ForwardUnsubscribeContext>();
                return chain.Invoke(new ForwardUnsubscribeContext(iface, routes.ToArray(), typeGenerator.GetType(context.MessageType), context));
            });

        return Task.WhenAll(forkTasks);
    }

    IRoutingProtocol routingProtocol;
    RuntimeTypeGenerator typeGenerator;
}