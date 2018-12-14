﻿using System;
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
        string replyToRouter = null;
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

        var outgoingMessage = new OutgoingMessage(context.MessageId, forwardedHeaders, context.ReceivedBody);
        if (replyTo != null)
        {
            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(replyTo));

            var chain = context.Chains.Get<PostroutingContext>();
            var forkContext = new PostroutingContext(operation, context);
            await chain.Invoke(forkContext).ConfigureAwait(false);
            await next(context).ConfigureAwait(false);
        }
        else if (replyToRouter != null)
        {
            var chain = context.Chains.Get<AnycastContext>();
            var forkContext = new AnycastContext(replyToRouter, outgoingMessage, DistributionStrategyScope.Send, context);
            await chain.Invoke(forkContext).ConfigureAwait(false);
        }
        else
        {
            throw new UnforwardableMessageException("The reply contains neither \'reply-to\' nor \'reply-to-router\' correlation parameters required to route the message.");
        }

        await next(context).ConfigureAwait(false);
    }
}