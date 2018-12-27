using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class ForwardSubscribeGatewayRule : ChainTerminator<ForwardSubscribeContext>
{
    string localAddress;
    string localEndpoint;

    public ForwardSubscribeGatewayRule(string localAddress, string localEndpoint)
    {
        this.localAddress = localAddress;
        this.localEndpoint = localEndpoint;
    }

    protected override async Task<bool> Terminate(ForwardSubscribeContext context)
    {
        var forwardedSubscribes = context.Routes.Where(r => r.Gateway != null);
        var forkContexts = forwardedSubscribes.Select(r =>
            new AnycastContext(r.Gateway,
                MessageDrivenPubSub.CreateMessage(r.Destination, context.MessageType, null, localEndpoint, MessageIntentEnum.Subscribe),
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
