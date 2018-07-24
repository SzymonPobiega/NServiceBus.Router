namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class CaptureOutgoingMessageRule : IRule<PostroutingContext, PostroutingContext>
    {
        public Task Invoke(PostroutingContext context, Func<PostroutingContext, Task> next)
        {
            if (context.Extensions.TryGet<List<CapturedOutgoingMessages>>(out var capturedMessages))
            {
                capturedMessages.Add(new CapturedOutgoingMessages(context.Interface, context.Messages));
                //Do not forward the invocation to the terminator
                return Task.CompletedTask;
            }
            return next(context);
        }
    }
}