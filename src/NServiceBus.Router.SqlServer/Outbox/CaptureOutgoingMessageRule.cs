namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Threading.Tasks;

    class CaptureOutgoingMessageRule : IRule<PostroutingContext, PostroutingContext>
    {
        DeduplicationSettings settings;

        public CaptureOutgoingMessageRule(DeduplicationSettings settings)
        {
            this.settings = settings;
        }

        public Task Invoke(PostroutingContext context, Func<PostroutingContext, Task> next)
        {
            if (context.DestinationEndpoint != null
                && context.Extensions.TryGet<CapturedTransportOperations>(out var capturedMessages)
                && settings.IsDeduplicationEnabledFor(capturedMessages.SqlInterface, context.Interface, context.DestinationEndpoint))
            {
                foreach (var operation in context.Messages)
                {
                    capturedMessages.Add(new CapturedTransportOperation(operation.Message, context.DestinationEndpoint));
                }

                //Do not forward the invocation to the terminator
                return Task.CompletedTask;
            }

            return next(context);
        }
    }
}