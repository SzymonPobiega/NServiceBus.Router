using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

class ReplyForwarder
{
    public Task Forward(MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher)
    {
        var forwardedHeaders = new Dictionary<string, string>(context.Headers);

        string replyTo = null;
        if (!forwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
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

        var outgoingMessage = new OutgoingMessage(context.MessageId, forwardedHeaders, context.Body);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(replyTo));
        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }
}