namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Threading.Tasks;
    using Transport;

    class InboxRule : IRule<RawContext, RawContext>
    {
        IInboxPersitence persitence;

        public InboxRule(IInboxPersitence persitence)
        {
            this.persitence = persitence;
        }

        public async Task Invoke(RawContext context, Func<RawContext, Task> next)
        {
            var dispatched = false;
            var transaction = await persitence.BeginTransaction();
            try
            {
                var duplicate = await persitence.Store(context.MessageId, context.Body, context.Headers, transaction.TransportTransaction);
                if (duplicate)
                {
                    return;
                }

                var receivedTransportTransaction = context.Extensions.Get<TransportTransaction>();

                context.Extensions.Set(transaction.TransportTransaction);

                await next(context);

                context.Extensions.Set(receivedTransportTransaction);

                dispatched = true;
            }
            finally
            {
                if (!dispatched)
                {
                    await transaction.Rollback().ConfigureAwait(false);
                }
                else
                {
                    await transaction.Commit().ConfigureAwait(false);
                }
            }
        }
    }
}