using System;
using System.Threading.Tasks;

namespace NServiceBus.Router.Resubscriber
{
    class UnsubscribeInterceptor : IRule<ForwardUnsubscribeContext, ForwardUnsubscribeContext>
    {
        IResubscriberStorage storage;

        public UnsubscribeInterceptor(IResubscriberStorage storage)
        {
            this.storage = storage;
        }

        public async Task Invoke(ForwardUnsubscribeContext context, Func<ForwardUnsubscribeContext, Task> next)
        {
            await next(context).ConfigureAwait(false);
            await storage.Unsubscribe(context.MessageType);
        }
    }
}