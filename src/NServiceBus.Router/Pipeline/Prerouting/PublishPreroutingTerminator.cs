using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class PublishPreroutingTerminator : ChainTerminator<PublishPreroutingContext>
{
    string[] allInterfaces;
    RuntimeTypeGenerator typeGenerator;

    public PublishPreroutingTerminator(string[] allInterfaces, RuntimeTypeGenerator typeGenerator)
    {
        this.allInterfaces = allInterfaces;
        this.typeGenerator = typeGenerator;
    }

    protected override async Task<bool> Terminate(PublishPreroutingContext context)
    {
        var outgoingInterfaces = allInterfaces.Where(i => i != context.IncomingInterface);

        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var forkTasks = outgoingInterfaces
            .Select(async iface =>
            {
                var chains = interfaces.GetChainsFor(iface);
                var chain = chains.Get<ForwardPublishContext>();
                var forwardPublishContext = new ForwardPublishContext(iface, typeGenerator.GetType(context.Types.First()), context);
                await chain.Invoke(forwardPublishContext).ConfigureAwait(false);

                return forwardPublishContext.Dropped || forwardPublishContext.Forwarded;
            });

        var results = await Task.WhenAll(forkTasks).ConfigureAwait(false);
        if (!results.Any())
        { 
            throw new UnforwardableMessageException($"The incoming message {context.MessageId} has not been forwarded. This might indicate a configuration problem.");
        }
        return true;
    }
}