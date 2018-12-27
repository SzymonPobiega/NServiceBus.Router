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
            UnwrapExistingCorrelationId(context);
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

        if (context.ForwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            newCorrelationId = newCorrelationId.AppendTLV("id", correlationId);
        }

        context.ForwardedHeaders[Headers.CorrelationId] = newCorrelationId;
        context.ForwardedHeaders[Headers.ReplyToAddress] = localAddress;
        context.ForwardedHeaders[RouterHeaders.ReplyToRouter] = endpointName;
    }

    static void UnwrapExistingCorrelationId(BaseForwardRuleContext context)
    {
        if (context.ForwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
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

                correlationId = temp;
            }

            context.ForwardedHeaders[Headers.CorrelationId] = correlationId;
        }
    }

}