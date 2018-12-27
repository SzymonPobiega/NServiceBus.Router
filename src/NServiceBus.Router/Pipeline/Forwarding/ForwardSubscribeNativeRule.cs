using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardSubscribeNativeRule : ChainTerminator<ForwardSubscribeContext>
{
    IManageSubscriptions subscriptionManager;

    public ForwardSubscribeNativeRule(IManageSubscriptions subscriptionManager)
    {
        this.subscriptionManager = subscriptionManager;
    }

    protected override async Task<bool> Terminate(ForwardSubscribeContext context)
    {
        await subscriptionManager.Subscribe(context.MessageRuntimeType, context).ConfigureAwait(false);

        return true;
    }
}