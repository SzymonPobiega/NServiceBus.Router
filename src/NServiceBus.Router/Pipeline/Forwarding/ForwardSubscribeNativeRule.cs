using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardSubscribeNativeRule : IRule<ForwardSubscribeContext, ForwardSubscribeContext>
{
    IManageSubscriptions subscriptionManager;

    public ForwardSubscribeNativeRule(IManageSubscriptions subscriptionManager)
    {
        this.subscriptionManager = subscriptionManager;
    }

    public async Task Invoke(ForwardSubscribeContext context, Func<ForwardSubscribeContext, Task> next)
    {
        await subscriptionManager.Subscribe(context.MessageRuntimeType, context).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}