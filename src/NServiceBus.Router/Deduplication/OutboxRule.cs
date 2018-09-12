namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using Transport;

    class OutboxRule : IRule<RawContext, RawContext>
    {
        OutboxPersister persister;
        OutboxCleanerCollection outboxCleanerCollection;
        Dispatcher dispatcher;

        public OutboxRule(OutboxPersister persister, OutboxCleanerCollection outboxCleanerCollection, Dispatcher dispatcher)
        {
            this.persister = persister;
            this.outboxCleanerCollection = outboxCleanerCollection;
            this.dispatcher = dispatcher;
        }

        public async Task Invoke(RawContext context, Func<RawContext, Task> next)
        {
            var capturedMessages = new List<CapturedTransportOperation>();
            context.Set(capturedMessages);

            await next(context).ConfigureAwait(false);

            context.Remove<List<CapturedTransportOperation>>();

            if (!capturedMessages.Any())
            {
                return;
            }

            var transportTransaction = context.Extensions.Get<TransportTransaction>();
            var connection = transportTransaction.Get<SqlConnection>();
            var transaction = transportTransaction.Get<SqlTransaction>();

            await persister.Store(capturedMessages, outboxCleanerCollection.UpdateInsertedSequence, connection, transaction).ConfigureAwait(false);

            foreach (var operation in capturedMessages)
            {
                dispatcher.Enqueue(operation);
            }
        }
    }
}
