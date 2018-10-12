using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Router.Deduplication;

class InboxCleanerCollection : IModule
{
    Dictionary<string, InboxCleaner> sequences;
    CancellationTokenSource tokenSource;

    public InboxCleanerCollection(SqlDeduplicationSettings settings, InboxPersister persistence)
    {
        sequences = settings.GetAllSources().ToDictionary(s => s, d => new InboxCleaner(d, persistence, settings.ConnFactory));
    }

    public void UpdateReceivedSequence(string sequenceKey, long sequenceValue)
    {
        sequences[sequenceKey].UpdateReceivedSequence(sequenceValue);
    }

    public (Task cleanBarrier, WatermarkCheckViolationResult checkResult) CheckAgainsWatermarks(string sequenceKey, long sequenceValue, Task currentCleanBarrier)
    {
        return sequences[sequenceKey].CheckConstraintFailedFor(sequenceValue, currentCleanBarrier);
    }

    public Task Start(RootContext rootContext)
    {
        tokenSource = new CancellationTokenSource();
        foreach (var sequence in sequences.Values)
        {
            sequence.Start(tokenSource.Token);
        }
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        tokenSource.Cancel();
        return Task.WhenAll(sequences.Values.Select(s => s.Stop()));
    }
}