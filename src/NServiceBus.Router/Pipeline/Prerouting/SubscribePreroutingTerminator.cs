using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class SubscribePreroutingTerminator : ChainTerminator<SubscribePreroutingContext>
{
    public SubscribePreroutingTerminator(string[] allInterfaces, IRoutingProtocol routingProtocol, RuntimeTypeGenerator typeGenerator)
    {
        this.allInterfaces = allInterfaces;
        this.routingProtocol = routingProtocol;
        this.typeGenerator = typeGenerator;
    }
    protected override async Task<bool> Terminate(SubscribePreroutingContext context)
    {
        var interfaces = context.Extensions.Get<IInterfaceChains>();

        //If no publisher is provided forward the subscribe to all outgoing interfaces
        var outgoingInterfaces = context.Destinations.Any()
            ? routingProtocol.RouteTable.GetOutgoingInterfaces(context.IncomingInterface, context.Destinations)
            : allInterfaces.Where(i => i != context.IncomingInterface);

        var routes = routingProtocol.RouteTable.Route(context.IncomingInterface, context.Destinations).ToArray();

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

    readonly string[] allInterfaces;
    IRoutingProtocol routingProtocol;
    RuntimeTypeGenerator typeGenerator;
}