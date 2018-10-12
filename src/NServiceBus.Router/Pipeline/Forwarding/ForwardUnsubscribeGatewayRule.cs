using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class ForwardUnsubscribeGatewayRule : IRule<ForwardUnsubscribeContext, ForwardUnsubscribeContext>
{
    string localAddress;
    string localEndpoint;

    public ForwardUnsubscribeGatewayRule(string localAddress, string localEndpoint)
    {
        this.localAddress = localAddress;
        this.localEndpoint = localEndpoint;
    }

    public async Task Invoke(ForwardUnsubscribeContext context, Func<ForwardUnsubscribeContext, Task> next)
    {
        var forwardedSubscribes = context.Routes.Where(r => r.Gateway != null);
        var forkContexts = forwardedSubscribes.Select(r =>
            new AnycastContext(r.Gateway,
                MessageDrivenPubSub.CreateMessage(r.Destination, context.MessageType, localAddress, localEndpoint, MessageIntentEnum.Unsubscribe),
                DistributionStrategyScope.Send,
                context));

        var chain = context.Chains.Get<AnycastContext>();
        var forkTasks = forkContexts.Select(c => chain.Invoke(c));
        await Task.WhenAll(forkTasks).ConfigureAwait(false);
        await next(context);
    }
}