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
        OutboxPersistence persistence;
        EpochManager epochManager;
        Dispatcher dispatcher;

        public OutboxRule(OutboxPersistence persistence, EpochManager epochManager, Dispatcher dispatcher)
        {
            this.persistence = persistence;
            this.epochManager = epochManager;
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

            await persistence.Store(capturedMessages, epochManager.UpdateInsertedSequence, connection, transaction).ConfigureAwait(false);

            foreach (var operation in capturedMessages)
            {
                dispatcher.Enqueue(operation);
            }
        }
    }
}
