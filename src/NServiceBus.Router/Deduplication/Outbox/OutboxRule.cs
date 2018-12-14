namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using Transport;

    class OutboxRule : IRule<RawContext, RawContext>
    {
        OutboxPersisterCollection outboxPersisterCollection;
        Dispatcher dispatcher;

        public OutboxRule(OutboxPersisterCollection outboxPersisterCollection, Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            this.outboxPersisterCollection = outboxPersisterCollection;
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
                throw new Exception("For the de-duplicating link to work the incoming interface has to use SQL Server transport in native transaction mode.");
            }

            await Store(capturedMessages, connection, transaction).ConfigureAwait(false);

            foreach (var operation in capturedMessages)
            {
                dispatcher.Enqueue(operation);
            }
        }

        async Task Store(List<CapturedTransportOperation> capturedMessages, SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var op in capturedMessages)
            {
                await outboxPersisterCollection.Store(op, connection, transaction).ConfigureAwait(false);
            }
        }

    }
}
