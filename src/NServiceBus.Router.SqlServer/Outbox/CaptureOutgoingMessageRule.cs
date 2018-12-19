namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Threading.Tasks;

    class CaptureOutgoingMessageRule : IRule<AnycastContext, AnycastContext>
    {
        DeduplicationSettings settings;

        public CaptureOutgoingMessageRule(DeduplicationSettings settings)
        {
            this.settings = settings;
        }

        public Task Invoke(AnycastContext context, Func<AnycastContext, Task> next)
        {
            if (context.Extensions.TryGet<CapturedTransportOperations>(out var capturedMessages)
                && settings.IsDeduplicationEnabledFor(capturedMessages.SqlInterface, context.Interface, context.DestinationEndpoint))
            {
                capturedMessages.Add(new CapturedTransportOperation(context.Message, context.DestinationEndpoint));
                //Do not forward the invocation to the terminator
                return Task.CompletedTask;
            }
            return next(context);
        }
    }
}