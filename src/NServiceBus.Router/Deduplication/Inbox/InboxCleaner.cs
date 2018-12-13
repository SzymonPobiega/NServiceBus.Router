namespace NServiceBus.Router.Deduplication.Inbox
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class InboxCleaner
    {
        string sourceKey;
        volatile LinkState linkState;
        long lastReceived;
        long cachedLo;
        long cachedHi;
        Task closeTask;
        InboxPersister persistence;
        Func<SqlConnection> connectionFactory;
        ILog logger = LogManager.GetLogger<InboxCleaner>();
        AsyncManualResetEvent @event = new AsyncManualResetEvent();
        volatile TaskCompletionSource<bool> cleanRunAwaitable = new TaskCompletionSource<bool>();

        public InboxCleaner(string sourceSequenceKey, InboxPersister persistence, Func<SqlConnection> connectionFactory)
        {
            this.sourceKey = sourceSequenceKey;
            this.persistence = persistence;
            this.connectionFactory = connectionFactory;
        }

        public void Start(CancellationToken token)
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

                            logger.Debug($"Attempting advance epoch for source {sourceKey} based on link state {linkState}");

                            var newState = await persistence.Advance(sourceKey, linkState, conn);

                            @event.Reset();

                            linkState = newState;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error("Unexpected error while trying to advance epoch", e);
                    }
                    finally
                    {
                        cleanRunAwaitable.SetResult(true);
                        cleanRunAwaitable = new TaskCompletionSource<bool>();
                    }
                }
            });
        }

        public async Task Stop()
        {
            @event.Cancel();
            try
            {
                await closeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
        }

        public void TriggerAdvance()
        {
            @event.Set();
        }

        public void UpdateReceivedSequence(long sequenceValue)
        {
            var highestSeenSequenceValue = InterlocedEx.ExchangeIfGreaterThan(ref lastReceived, sequenceValue);
            var localHi = Interlocked.Read(ref cachedHi);
            var localLo = Interlocked.Read(ref cachedLo);

            //Triggers the closing process if the last received sequence number is in the upper quarter of the window
            var epochSize = localHi - localLo;
            if (highestSeenSequenceValue >= localLo + epochSize / 2 + epochSize / 4)
            {
                @event.Set();
            }
        }

        public LinkState GetLinkState()
        {
            return linkState;
        }
    }
}