using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class ForwardUnsubscribeMessageDrivenRule : ChainTerminator<ForwardUnsubscribeContext>
{
    string localAddress;
    string localEndpoint;

    public ForwardUnsubscribeMessageDrivenRule(string localAddress, string localEndpoint)
    {
        this.localAddress = localAddress;
        this.localEndpoint = localEndpoint;
    }

    protected override async Task<bool> Terminate(ForwardUnsubscribeContext context)
    {
        var immediateSubscribes = context.Routes.Where(r => r.Gateway == null);
        var forkContexts = immediateSubscribes.Select(r =>
            new MulticastContext(r.Destination,
                MessageDrivenPubSub.CreateMessage(null, context.MessageType, localAddress, localEndpoint, MessageIntent.Unsubscribe), context))
            .ToArray();

        if (forkContexts.Any())
        {
            var chain = context.Chains.Get<MulticastContext>();
            var forkTasks = forkContexts.Select(c => chain.Invoke(c));
            await Task.WhenAll(forkTasks).ConfigureAwait(false);

            return true;
        }

        return false;
    }
}