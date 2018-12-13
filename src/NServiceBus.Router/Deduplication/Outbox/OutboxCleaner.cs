using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Transport;

namespace NServiceBus.Router.Deduplication
{
    class OutboxCleaner
    {
        string destinationKey;
        volatile LinkState linkState;
        Task closeTask;
        OutboxPersister persister;
        Func<SqlConnection> connectionFactory;
        ILog logger = LogManager.GetLogger<OutboxCleaner>();
        AsyncManualResetEvent @event = new AsyncManualResetEvent();

        public OutboxCleaner(string destinationKey, OutboxPersister persister, Func<SqlConnection> connectionFactory)
        {
            this.destinationKey = destinationKey;
            this.persister = persister;
            this.connectionFactory = connectionFactory;
        }

        public void Start(CancellationToken token, Func<OutgoingMessage, Task> dispatch)
        {
            closeTask = Task.Run(async () =>
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
                            await conn.OpenAsync().ConfigureAwait(false);

                            logger.Debug($"Attempting advance epoch for destination {destinationKey} based on link state {linkState}.");

                            var newState = await persister.TryClose(destinationKey, linkState, dispatch, conn);

                            @event.Reset();

                            linkState = newState;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error("Unexpected error while trying to advance epoch", e);
                    }
                }
            });
        }

        public LinkState GetLinkState()
        {
            return linkState;
        }

        public async Task Stop()
        {
            @event.Cancel();
            if (closeTask == null)
            {
                return;
            }
            try
            {
                await closeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
        }

        public void ValidateSequence(CapturedTransportOperation operation)
        {
            var localState = linkState;

            if (operation.Sequence >= localState.HeadSession.Hi)
            {
                //The sequence number is higher than the head session hi value. We need to update link state or advance the epoch
                @event.Set();
                //Trigger retries
                throw new Exception("The generated sequence value is higher than current epoch allows. Triggering advancing of epoch.");
            }

            if (operation.Sequence < localState.TailSession.Lo)
            {
                throw new Exception("The generated sequence value is lower than current epoch allows. Retrying.");
            }

            var epochSize = localState.HeadSession.Hi - localState.HeadSession.Lo;
            var threshold = localState.HeadSession.Lo + epochSize / 2;

            //Triggers the advancing the epoch if the last received sequence number is in the upper half of the head sequence
            if (operation.Sequence > threshold)
            {
                @event.Set();
            }

            var tableName = localState.GetTableName(operation.Sequence);
            operation.AssignTable(tableName);
        }
    }
}