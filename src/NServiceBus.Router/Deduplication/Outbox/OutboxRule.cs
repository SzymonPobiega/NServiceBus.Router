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

            if (!transportTransaction.TryGet<SqlConnection>(out var connection)
                || !transportTransaction.TryGet<SqlTransaction>(out var transaction))
            {
                throw new Exception("For the deduplicating link to work the incoming interface has to use SQL Server transport in native transaction mode.");
            }

            await persister.Store(capturedMessages, outboxCleanerCollection.ValidateSequence, connection, transaction).ConfigureAwait(false);

            foreach (var operation in capturedMessages)
            {
                dispatcher.Enqueue(operation);
            }
        }
    }
}
