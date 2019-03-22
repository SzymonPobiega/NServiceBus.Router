using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;

class ForwardReplyRule : ChainTerminator<ForwardReplyContext>
{
    protected override async Task<bool> Terminate(ForwardReplyContext context)
    {
        string replyTo = null;
        string replyToRouter = null;
        string unwrappedCorrelationId = null;
        if (!context.ForwardedHeaders.TryGetValue(RouterHeaders.PreviousCorrelationId, out var correlationId))
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
                    unwrappedCorrelationId = v;
                }
                if (t == "reply-to-router")
                {
                    replyToRouter = v;
                }
            });
        }
        catch (Exception e)
        {
            throw new UnforwardableMessageException($"Cannot decode value in '{Headers.CorrelationId}' header: " + e.Message);
        }

        var outgoingMessage = new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody);
        if (replyTo != null)
        {
            //Copy the correlation ID header which contains the route the reply underwent to previous header to preserve it
            context.ForwardedHeaders[RouterHeaders.PreviousCorrelationId] = context.ForwardedHeaders[Headers.CorrelationId];

            //Update the correlation ID 
            context.ForwardedHeaders[Headers.CorrelationId] = unwrappedCorrelationId ?? correlationId;
            context.ForwardedHeaders.Remove(RouterHeaders.ReplyToRouter);

            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(replyTo));
            var chain = context.Chains.Get<PostroutingContext>();
            var forkContext = new PostroutingContext(null, operation, context);
            await chain.Invoke(forkContext).ConfigureAwait(false);

            return true;
        }
        if (replyToRouter != null)
        {
            context.ForwardedHeaders[RouterHeaders.PreviousCorrelationId] = unwrappedCorrelationId ?? correlationId;
            var chain = context.Chains.Get<AnycastContext>();
            var forkContext = new AnycastContext(replyToRouter, outgoingMessage, DistributionStrategyScope.Send, context);

            await chain.Invoke(forkContext).ConfigureAwait(false);

            return true;
        }

        throw new UnforwardableMessageException("The reply contains neither \'reply-to\' nor \'reply-to-router\' correlation parameters required to route the message.");
    }
}