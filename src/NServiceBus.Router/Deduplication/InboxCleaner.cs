using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;

class InboxCleaner
{
    string sourceSequenceKey;
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
        this.sourceSequenceKey = sourceSequenceKey;
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

                        logger.Debug($"Attempting to close the inbox table for sequence {sourceSequenceKey} based on lo={cachedLo} and hi={cachedHi}");

                        var (newLo, newHi) = await persistence.TryClose(sourceSequenceKey, cachedLo, cachedHi, conn);

                        logger.Debug($"New watermark values for inbox for {sourceSequenceKey} lo={cachedLo} and hi={cachedHi}");

                        @event.Reset();
                        cachedLo = newLo;
                        cachedHi = newHi;
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Unexpected error while closing the inbox table", e);
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

    public (Task cleanBarrier, WatermarkCheckViolationResult checkResult) CheckConstraintFailedFor(long sequenceValue, Task currentCleanBarrier)
    {
        var localLo = Interlocked.Read(ref cachedLo);
        if (sequenceValue < localLo)
        {
            return (currentCleanBarrier, WatermarkCheckViolationResult.Duplicate);
        }
        //Seems like our watermarks values are stale or the message sequence number does not fit. Trigger closing.
        @event.Set();

        return (cleanRunAwaitable.Task, WatermarkCheckViolationResult.Retry);
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
}