using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class ForwardUnsubscribeGatewayRule : ChainTerminator<ForwardUnsubscribeContext>
{
    string localAddress;
    string localEndpoint;

    public ForwardUnsubscribeGatewayRule(string localAddress, string localEndpoint)
    {
        this.localAddress = localAddress;
        this.localEndpoint = localEndpoint;
    }

    protected override async Task<bool> Terminate(ForwardUnsubscribeContext context)
    {
        var forwardedSubscribes = context.Routes.Where(r => r.Gateway != null);
        var forkContexts = forwardedSubscribes.Select(r =>
            new AnycastContext(r.Gateway,
                MessageDrivenPubSub.CreateMessage(r.Destination, context.MessageType, null, localEndpoint, MessageIntentEnum.Unsubscribe),
                DistributionStrategyScope.Send,
                context)).ToArray();

        if (!forkContexts.Any())
        {
            return false;
        }
        var chain = context.Chains.Get<AnycastContext>();
        var forkTasks = forkContexts.Select(c => chain.Invoke(c));
        await Task.WhenAll(forkTasks).ConfigureAwait(false);

        return true;
    }
}