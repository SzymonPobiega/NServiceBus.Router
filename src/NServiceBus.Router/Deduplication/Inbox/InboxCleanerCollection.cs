namespace NServiceBus.Router.Deduplication.Inbox
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    class InboxCleanerCollection : IModule
    {
        Dictionary<string, InboxCleaner> sequences;
        CancellationTokenSource tokenSource;

        public InboxCleanerCollection(DeduplicationSettings settings, InboxPersister persistence)
        {
            sequences = settings.GetAllSources().ToDictionary(s => s, d => new InboxCleaner(d, persistence, settings.ConnFactory));
        }

        public void UpdateReceivedSequence(string sequenceKey, long sequenceValue)
        {
            sequences[sequenceKey].UpdateReceivedSequence(sequenceValue);
        }

        public LinkState GetLinkState(string sequenceKey)
        {
            return sequences[sequenceKey].GetLinkState();
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
}