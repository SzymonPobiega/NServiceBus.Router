using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Transport;

class ForwardSendRule : IRule<ForwardSendContext, ForwardSendContext>
{
    string localAddress;

    public ForwardSendRule(string localAddress)
    {
        this.localAddress = localAddress;
    }

    public async Task Invoke(ForwardSendContext context, Func<ForwardSendContext, Task> next)
    {
        AnycastContext Forward(Route r)
        {
            return r.Gateway != null
                ? CreateForkContext(context, r.Gateway, r.Destination)
                : CreateForkContext(context, r.Destination, null);
        }

        var forkContexts = context.Routes.Select(Forward).ToArray();
        var chain = context.Chains.Get<AnycastContext>();
        var forkTasks = forkContexts.Select(c => chain.Invoke(c));
        await Task.WhenAll(forkTasks).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }

    AnycastContext CreateForkContext(ForwardSendContext context, string destinationEndpoint, string ultimateDestination)
    {
        var forwardedHeaders = context.ReceivedHeaders.Copy();

        if (context.ReceivedHeaders.TryGetValue(Headers.ReplyToAddress, out var replyToHeader)
            && context.ReceivedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            // pipe-separated TLV format
            var newCorrelationId = TLV
                .Encode("id", correlationId)
                .AppendTLV("reply-to", replyToHeader)
                .AppendTLV("iface", context.IncomingInterface);

            forwardedHeaders[Headers.CorrelationId] = newCorrelationId;
        }
        forwardedHeaders[Headers.ReplyToAddress] = localAddress;

        if (ultimateDestination != null)
        {
            forwardedHeaders["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
        }

        var message = new OutgoingMessage(context.MessageId, forwardedHeaders, context.ReceivedBody);
        return new AnycastContext(destinationEndpoint, message, DistributionStrategyScope.Send, context);
    }
}