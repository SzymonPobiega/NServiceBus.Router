using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;

class ForwardPublishNativeRule : ChainTerminator<ForwardPublishContext>
{
    protected override async Task<bool> Terminate(ForwardPublishContext context)
    {
        var addressTag = new MulticastAddressTag(context.RootEventType);
        var outgoingMessage = new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody);
        var operation = new TransportOperation(outgoingMessage, addressTag);

        var forkContext = new PostroutingContext(null, operation, context);
        var chain = context.Chains.Get<PostroutingContext>();
        await chain.Invoke(forkContext).ConfigureAwait(false);

        return true;
    }
}