using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardUnsubscribeNativeRule : ChainTerminator<ForwardUnsubscribeContext>
{
    IManageSubscriptions subscriptionManager;

    public ForwardUnsubscribeNativeRule(IManageSubscriptions subscriptionManager)
    {
        this.subscriptionManager = subscriptionManager;
    }

    protected override async Task<bool> Terminate(ForwardUnsubscribeContext context)
    {
        await subscriptionManager.Unsubscribe(context.MessageRuntimeType, context).ConfigureAwait(false);

        return true;
    }
}