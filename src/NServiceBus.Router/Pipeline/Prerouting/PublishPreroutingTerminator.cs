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

        var verificationState = new PostroutingVerificationRule.State(context.MessageId);

        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var forkTasks = outgoingInterfaces
            .Select(iface =>
            {
                var chains = interfaces.GetChainsFor(iface);
                var chain = chains.Get<ForwardPublishContext>();
                var forwardPublishContext = new ForwardPublishContext(iface, typeGenerator.GetType(context.Types.First()), context);
                forwardPublishContext.Extensions.Set(verificationState);
                return chain.Invoke(forwardPublishContext);
            });

        await Task.WhenAll(forkTasks).ConfigureAwait(false);

        if (!verificationState.HasBeenForwarded)
        {
            throw new UnforwardableMessageException($"The incoming message {context.MessageId} has not been forwarded. This might indicate a configuration problem.");
        }

        return true;
    }
}