using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Transport;

class OutboxCleaner
{
    string sequenceKey;
    long lastInserted;
    long lo;
    long hi;
    Task closeTask;
    OutboxPersister persister;
    Func<SqlConnection> connectionFactory;
    ILog logger = LogManager.GetLogger<OutboxCleaner>();
    AsyncManualResetEvent @event = new AsyncManualResetEvent();

    public OutboxCleaner(string sequenceKey, OutboxPersister persister, Func<SqlConnection> connectionFactory)
    {
        this.sequenceKey = sequenceKey;
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

                        logger.Debug($"Attempting to close the outbox table for sequence {sequenceKey} based on lo={lo} and hi={hi}");

                        var (newLo, newHi) = await persister.TryClose(sequenceKey, lo, hi, dispatch, conn);

                        logger.Debug($"New watermark values for outbox for {sequenceKey} are lo={lo} and hi={hi}");

                        @event.Reset();
                        lo = newLo;
                        hi = newHi;
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Unexpected error while closing the outbox table", e);
                }
            }
        });
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

    public void UpdateInsertedSequence(long sequenceValue)
    {
        var highestSeenSequenceValue = InterlocedEx.ExchangeIfGreaterThan(ref lastInserted, sequenceValue);
        var localHi = Interlocked.Read(ref hi);
        var localLo = Interlocked.Read(ref lo);

        var epochSize = localHi - localLo;

        //Triggers the closing process if the last received sequence number is in the upper quarter of the window
        if (highestSeenSequenceValue >= localLo + epochSize / 2 + epochSize / 4)
        {
            @event.Set();
        }
    }
}