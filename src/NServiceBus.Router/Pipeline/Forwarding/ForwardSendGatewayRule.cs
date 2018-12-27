using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardSendGatewayRule : ChainTerminator<ForwardSendContext>
{
    protected override async Task<bool> Terminate(ForwardSendContext context)
    {
        var forkContexts = context.Routes.Where(r => r.Gateway != null).Select(r => CreateForkContext(context, r.Gateway, r.Destination)).ToArray();
        if (!forkContexts.Any())
        {
            return false;
        }
        var chain = context.Chains.Get<AnycastContext>();
        var forkTasks = forkContexts.Select(c => chain.Invoke(c));
        await Task.WhenAll(forkTasks).ConfigureAwait(false);

        return true;

    }

    AnycastContext CreateForkContext(ForwardSendContext context, string gateway, string ultimateDestination)
    {
        if (ultimateDestination != null)
        {
            context.ForwardedHeaders["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
        }

        var message = new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody);
        return new AnycastContext(gateway, message, DistributionStrategyScope.Send, context);
    }
}