using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;
using NServiceBus.Unicast.Messages;

class ForwardUnsubscribeNativeRule : ChainTerminator<ForwardUnsubscribeContext>
{
    ISubscriptionManager subscriptionManager;

    public ForwardUnsubscribeNativeRule(ISubscriptionManager subscriptionManager)
    {
        this.subscriptionManager = subscriptionManager;
    }

    protected override async Task<bool> Terminate(ForwardUnsubscribeContext context)
    {
        await subscriptionManager.Unsubscribe(new MessageMetadata(context.MessageRuntimeType), context).ConfigureAwait(false);

        return true;
    }
}