using System;
using System.Threading.Tasks;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

class CorrelationIdForReplyBehavior : Behavior<IOutgoingReplyContext>
{
    public override Task Invoke(IOutgoingReplyContext context, Func<Task> next)
    {
        if (context.Extensions.TryGet<IncomingMessage>(out var incomingMessage)
            && incomingMessage.Headers.TryGetValue(RouterHeaders.PreviousCorrelationId, out var previousCorrelation))
        {
            context.Headers[RouterHeaders.PreviousCorrelationId] = previousCorrelation;
        }

        return next();
    }
}