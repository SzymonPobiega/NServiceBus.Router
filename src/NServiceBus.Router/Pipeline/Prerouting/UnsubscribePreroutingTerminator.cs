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
    protected override async Task<bool> Terminate(UnsubscribePreroutingContext context)
    {
        if (!context.Destinations.Any())
        {
            return false;
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

        await Task.WhenAll(forkTasks).ConfigureAwait(false);

        return true;
    }

    IRoutingProtocol routingProtocol;
    RuntimeTypeGenerator typeGenerator;
}