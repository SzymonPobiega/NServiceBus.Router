namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Collections.Generic;
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
            if (!settings.IsOutboxEnabledFor(context.Interface))
            {
                return next(context);
            }

            if (context.Extensions.TryGet<List<CapturedTransportOperation>>(out var capturedMessages))
            {
                if (!settings.IsOutboxEnabledFor(context.Interface, context.DestinationEndpoint))
                {
                    throw new Exception($"Total ordering is not enabled for destination {context.DestinationEndpoint} via interface {context.Interface}.");
                }
                capturedMessages.Add(new CapturedTransportOperation(context.Message, context.DestinationEndpoint));

                //Do not forward the invocation to the terminator
                return Task.CompletedTask;
            }
            return next(context);
        }
    }
}