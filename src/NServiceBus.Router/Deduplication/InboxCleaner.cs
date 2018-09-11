using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;

class InboxCleaner
{
    string sourceSequenceKey;
    long lastReceived;
    long lo;
    long hi;
    Task closeTask;
    InboxPersitence persistence;
    Func<SqlConnection> connectionFactory;
    ILog logger = LogManager.GetLogger<InboxCleaner>();
    AsyncManualResetEvent @event = new AsyncManualResetEvent();
    volatile TaskCompletionSource<bool> cleanRunAwaitable = new TaskCompletionSource<bool>();

    public InboxCleaner(string sourceSequenceKey, InboxPersitence persistence, Func<SqlConnection> connectionFactory)
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

                        logger.Debug($"Attempting to close epoch for sequence {sourceSequenceKey} based on lo={lo} and hi={hi}");

                        var (newLo, newHi) = await persistence.TryClose(sourceSequenceKey, lo, hi, conn);

                        logger.Debug($"New values lo={lo} and hi={hi}");

                        @event.Reset();
                        lo = newLo;
                        hi = newHi;
                    }
                }
                catch (Exception e)
                {
                    logger.Error("Unexpected error while closing the epoch", e);
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
        @event.Set();
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
        var localLo = Interlocked.Read(ref lo);
        if (sequenceValue < localLo)
        {
            return (currentCleanBarrier, WatermarkCheckViolationResult.Duplicate);
        }
        //Seems like our watermarks are off or the message sequence number does not fit. Trigger closing
        @event.Set();

        return (cleanRunAwaitable.Task, WatermarkCheckViolationResult.Retry);
    }

    public void UpdateReceivedSequence(long sequenceValue)
    {
        var updated = InterlockedExchangeIfGreaterThan(ref lastReceived, sequenceValue);
        var localHi = Interlocked.Read(ref hi);
        var localLo = Interlocked.Read(ref lo);

        var epochSize = localHi - localLo;
        if (updated < localLo + epochSize / 2 + epochSize / 4) //We have not got to the upper half of the upper epoch
        {
            return;
        }

        @event.Set();
    }

    static long InterlockedExchangeIfGreaterThan(ref long location, long newValue)
    {
        long initialValue;
        do
        {
            initialValue = Interlocked.Read(ref location);
            if (initialValue >= newValue)
            {
                return initialValue;
            }
        }
        while (Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);
        return initialValue;
    }
}