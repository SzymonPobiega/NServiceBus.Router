namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Transport;

    class OutboxPersisterRunner
    {
        static ILog log = LogManager.GetLogger<OutboxPersister>();

        Func<SqlConnection> connectionFactory;
        AsyncManualResetEvent @event = new AsyncManualResetEvent();
        OutboxPersister persister;
        Task advanceTask;

        public OutboxPersisterRunner(OutboxPersister persister, Func<SqlConnection> connectionFactory)
        {
            this.persister = persister;
            this.connectionFactory = connectionFactory;
        }

        public Task Store(CapturedTransportOperation operation, SqlConnection conn, SqlTransaction trans)
        {
            return persister.Store(operation, () => @event.Set(), conn, trans);
        }

        public async Task Start(CancellationToken token, Func<OutgoingMessage, Task> dispatch)
        {
            using (var conn = connectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                await persister.Initialize(dispatch, conn).ConfigureAwait(false);
            }
            advanceTask = Task.Run(async () => { await AdvanceLoop(token, dispatch); });
        }

        public async Task Stop()
        {
            @event.Cancel();
            if (advanceTask == null)
            {
                return;
            }
            try
            {
                await advanceTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
        }

        async Task AdvanceLoop(CancellationToken token, Func<OutgoingMessage, Task> dispatch)
        {
            while (!token.IsCancellationRequested)
            {
                await @event.WaitAsync().ConfigureAwait(false);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    using (var conn = connectionFactory())
                    {
                        await conn.OpenAsync(token).ConfigureAwait(false);

                        await persister.TryAdvance(dispatch, conn).ConfigureAwait(false);

                        @event.Reset();
                    }
                }
                catch (Exception e)
                {
                    log.Error("Unexpected error while trying to advance epoch", e);
                    //Trigger next attempt immediately
                    @event.Set();
                }
            }
        }
    }
}