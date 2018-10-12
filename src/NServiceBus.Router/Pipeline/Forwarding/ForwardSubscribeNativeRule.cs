using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardSubscribeNativeRule : IRule<ForwardSubscribeContext, ForwardSubscribeContext>
{
    IManageSubscriptions subscriptionManager;
    RuntimeTypeGenerator typeGenerator;

    public ForwardSubscribeNativeRule(IManageSubscriptions subscriptionManager, RuntimeTypeGenerator typeGenerator)
    {
        this.subscriptionManager = subscriptionManager;
        this.typeGenerator = typeGenerator;
    }

    public async Task Invoke(ForwardSubscribeContext context, Func<ForwardSubscribeContext, Task> next)
    {
        var type = typeGenerator.GetType(context.MessageType);
        await subscriptionManager.Subscribe(type, context).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}