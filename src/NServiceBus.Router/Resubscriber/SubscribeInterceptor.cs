using System;
using System.Threading.Tasks;

namespace NServiceBus.Router.Resubscriber
{
    class SubscribeInterceptor : IRule<ForwardSubscribeContext, ForwardSubscribeContext>
    {
        IResubscriberStorage storage;

        public SubscribeInterceptor(IResubscriberStorage storage)
        {
            this.storage = storage;
        }

        public async Task Invoke(ForwardSubscribeContext context, Func<ForwardSubscribeContext, Task> next)
        {
            await next(context).ConfigureAwait(false);
            await storage.Subscribe(context.MessageType);
        }
    }
}