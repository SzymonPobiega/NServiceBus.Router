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
        var forkContexts = context.Routes.Where(r => r.Gateway == null).Select(r => CreateForkContext(context, r.Destination)).ToArray();
        var chain = context.Chains.Get<AnycastContext>();
        var forkTasks = forkContexts.Select(c => chain.Invoke(c));
        await Task.WhenAll(forkTasks).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }

    AnycastContext CreateForkContext(ForwardSendContext context, string destinationEndpoint)
    {
        var forwardedHeaders = context.ReceivedHeaders.Copy();

        var newCorrelationId = TLV
            .Encode("iface", context.IncomingInterface);

        if (context.ReceivedHeaders.TryGetValue(RouterHeaders.ReplyToRouter, out var replyToRouter))
        {
            newCorrelationId = newCorrelationId.AppendTLV("reply-to-router", replyToRouter);
        }

        if (context.ReceivedHeaders.TryGetValue(Headers.ReplyToAddress, out var replyToHeader))
        {
            newCorrelationId = newCorrelationId.AppendTLV("reply-to", replyToHeader);
        }

        if (context.ReceivedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            newCorrelationId = newCorrelationId.AppendTLV("id", correlationId);
        }

        forwardedHeaders[Headers.CorrelationId] = newCorrelationId;
        forwardedHeaders[Headers.ReplyToAddress] = localAddress;
        
        var message = new OutgoingMessage(context.MessageId, forwardedHeaders, context.ReceivedBody);
        return new AnycastContext(destinationEndpoint, message, DistributionStrategyScope.Send, context);
    }
}