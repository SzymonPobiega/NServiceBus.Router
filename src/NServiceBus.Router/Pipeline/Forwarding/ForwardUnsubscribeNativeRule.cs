using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardUnsubscribeNativeRule : IRule<ForwardUnsubscribeContext, ForwardUnsubscribeContext>
{
    IManageSubscriptions subscriptionManager;

    public ForwardUnsubscribeNativeRule(IManageSubscriptions subscriptionManager)
    {
        this.subscriptionManager = subscriptionManager;
    }

    public async Task Invoke(ForwardUnsubscribeContext context, Func<ForwardUnsubscribeContext, Task> next)
    {
        await subscriptionManager.Unsubscribe(context.MessageRuntimeType, context).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}