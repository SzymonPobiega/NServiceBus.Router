namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Routing;

    class CaptureOutgoingMessageRule : IRule<PostroutingContext, PostroutingContext>
    {
        SqlDeduplicationSettings settings;

        public CaptureOutgoingMessageRule(SqlDeduplicationSettings settings)
        {
            this.settings = settings;
        }

        public Task Invoke(PostroutingContext context, Func<PostroutingContext, Task> next)
        {
            if (!settings.IsOutboxEnabledFor(context.Interface))
            {
                return next(context);
            }

            if (context.Extensions.TryGet<List<CapturedTransportOperation>>(out var capturedMessages))
            {
                foreach (var operation in context.Messages)
                {
                    if (!(operation.AddressTag is UnicastAddressTag unicastTag))
                    {
                        throw new Exception("Only unicast operations can be ordered.");
                    }
                    if (!settings.IsOutboxEnabledFor(context.Interface, unicastTag.Destination))
                    {
                        throw new Exception($"Total ordering is not enabled for destination {unicastTag.Destination} via interface {context.Interface}.");
                    }
                    capturedMessages.Add(new CapturedTransportOperation(operation, unicastTag.Destination));
                }
                
                //Do not forward the invocation to the terminator
                return Task.CompletedTask;
            }
            return next(context);
        }
    }
}