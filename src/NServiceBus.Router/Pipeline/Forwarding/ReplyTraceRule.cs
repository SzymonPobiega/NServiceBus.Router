using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class SendReplyTraceRule : ReplyTraceRule, IRule<ForwardSendContext, ForwardSendContext>
{
    public SendReplyTraceRule(string localAddress, string endpointName) 
        : base(localAddress, endpointName)
    {
    }

    public Task Invoke(ForwardSendContext context, Func<ForwardSendContext, Task> next)
    {
        AddTraceHeadersForRoutingBackReply(context);
        return next(context);
    }
}

class PublishReplyTraceRule : ReplyTraceRule, IRule<ForwardPublishContext, ForwardPublishContext>
{
    public PublishReplyTraceRule(string localAddress, string endpointName) 
        : base(localAddress, endpointName)
    {
    }

    public Task Invoke(ForwardPublishContext context, Func<ForwardPublishContext, Task> next)
    {
        AddTraceHeadersForRoutingBackReply(context);
        return next(context);
    }
}

class ReplyReplyTraceRule : ReplyTraceRule, IRule<ForwardReplyContext, ForwardReplyContext>
{
    public ReplyReplyTraceRule(string localAddress, string endpointName)
        : base(localAddress, endpointName)
    {
    }

    public Task Invoke(ForwardReplyContext context, Func<ForwardReplyContext, Task> next)
    {
        AddTraceHeadersForRoutingBackReply(context);
        return next(context);
    }
}

abstract class ReplyTraceRule
{
    string localAddress;
    string endpointName;

    protected ReplyTraceRule(string localAddress, string endpointName)
    {
        this.localAddress = localAddress;
        this.endpointName = endpointName;
    }

    protected void AddTraceHeadersForRoutingBackReply(BaseForwardRuleContext context)
    {
        if (!context.ForwardedHeaders.ContainsKey(RouterHeaders.ReplyToRouter))
        {
            UnwrapCorrelationIdAndSetTraceHeader(context);
        }

        var newCorrelationId = TLV
            .Encode("iface", context.IncomingInterface);

        if (context.ForwardedHeaders.TryGetValue(RouterHeaders.ReplyToRouter, out var replyToRouter))
        {
            newCorrelationId = newCorrelationId.AppendTLV("reply-to-router", replyToRouter);
        }
        else if (context.ForwardedHeaders.TryGetValue(Headers.ReplyToAddress, out var replyToHeader))
        {
            newCorrelationId = newCorrelationId.AppendTLV("reply-to", replyToHeader);
        }

        if (context.ForwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId) && correlationId != null)
        {
            newCorrelationId = newCorrelationId.AppendTLV("id", correlationId);
        }

        context.ForwardedHeaders[Headers.CorrelationId] = newCorrelationId;
        context.ForwardedHeaders[Headers.ReplyToAddress] = localAddress;
        context.ForwardedHeaders[RouterHeaders.ReplyToRouter] = endpointName;
    }

    /// <summary>
    /// Invoked when forwarding a message that was sent in context of a message forwarded previously by the Router. Such
    /// message contains the TLV-type correlation ID that contains the path of the message. The CorrelationID header
    /// need to be re-set and the path is copied to the trace header in order to allow the reply to a reply to be routed.
    /// </summary>
    static void UnwrapCorrelationIdAndSetTraceHeader(BaseForwardRuleContext context)
    {
        if (context.ForwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId) && correlationId != null)
        {
            while (true)
            {
                string temp = null;
                if (!correlationId.TryDecodeTLV((t, v) =>
                {
                    if (t == "id")
                    {
                        temp = v;
                    }
                }) || temp == null)
                {
                    break;
                }

                context.ForwardedHeaders[RouterHeaders.ReplyToTrace] = context.ForwardedHeaders[Headers.CorrelationId];
                correlationId = temp;
            }

            context.ForwardedHeaders[Headers.CorrelationId] = correlationId;
        }
    }

}