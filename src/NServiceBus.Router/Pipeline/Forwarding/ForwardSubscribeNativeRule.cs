using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;
using NServiceBus.Unicast.Messages;

class ForwardSubscribeNativeRule : ChainTerminator<ForwardSubscribeContext>
{
    ISubscriptionManager subscriptionManager;

    public ForwardSubscribeNativeRule(ISubscriptionManager subscriptionManager)
    {
        this.subscriptionManager = subscriptionManager;
    }

    protected override async Task<bool> Terminate(ForwardSubscribeContext context)
    {
        await subscriptionManager.SubscribeAll(new[]{new MessageMetadata(context.MessageRuntimeType) }, context).ConfigureAwait(false);

        return true;
    }
}