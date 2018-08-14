using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;

class ForwardReplyRule : IRule<ForwardReplyContext, ForwardReplyContext>
{
    public async Task Invoke(ForwardReplyContext context, Func<ForwardReplyContext, Task> next)
    {
        var forwardedHeaders = context.ReceivedHeaders.Copy();
        string replyTo = null;
        if (!context.ReceivedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            throw new UnforwardableMessageException($"The reply has to contain a '{Headers.CorrelationId}' header set by the router connector when sending out the initial message.");
        }

        try
        {
            correlationId.DecodeTLV((t, v) =>
            {
                if (t == "reply-to")
                {
                    replyTo = v;
                }
                if (t == "id")
                {
                    forwardedHeaders[Headers.CorrelationId] = v;
                }
            });
        }
        catch (Exception e)
        {
            throw new UnforwardableMessageException($"Cannot decode value in '{Headers.CorrelationId}' header: " + e.Message);
        }

        if (replyTo == null)
        {
            throw new UnforwardableMessageException("The reply message does not contain \'reply-to\' correlation parameter required to route the message.");
        }

        var outgoingMessage = new OutgoingMessage(context.MessageId, forwardedHeaders, context.ReceivedBody);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(replyTo));

        var chain = context.Chains.Get<PostroutingContext>();
        var forkContext = new PostroutingContext(operation, context);
        await chain.Invoke(forkContext).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}