using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Router.Deduplication;

class EpochManager : IModule
{
    Dictionary<string, SequenceEpochManager> sequences;
    CancellationTokenSource tokenSource;

    public EpochManager(SqlDeduplicationSettings settings, OutboxPersistence persistence, Func<SqlConnection> connectionFactory)
    {
        sequences = settings.GetAllDestinations().ToDictionary(d => d, d => new SequenceEpochManager(d, persistence, connectionFactory));
    }

    public void UpdateInsertedSequence(string sequenceKey, long sequenceValue)
    {
        sequences[sequenceKey].UpdateInsertedSequence(sequenceValue);
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