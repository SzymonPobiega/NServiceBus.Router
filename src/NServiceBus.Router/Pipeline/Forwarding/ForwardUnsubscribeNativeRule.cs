using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardUnsubscribeNativeRule : IRule<ForwardUnsubscribeContext, ForwardUnsubscribeContext>
{
    IManageSubscriptions subscriptionManager;
    RuntimeTypeGenerator typeGenerator;

    public ForwardUnsubscribeNativeRule(IManageSubscriptions subscriptionManager, RuntimeTypeGenerator typeGenerator)
    {
        this.subscriptionManager = subscriptionManager;
        this.typeGenerator = typeGenerator;
    }

    public async Task Invoke(ForwardUnsubscribeContext context, Func<ForwardUnsubscribeContext, Task> next)
    {
        var type = typeGenerator.GetType(context.MessageType);
        await subscriptionManager.Unsubscribe(type, context).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}