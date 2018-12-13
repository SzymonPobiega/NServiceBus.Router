using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;

namespace NServiceBus.Router.Deduplication
{
    class OutboxCleanerCollection : IModule
    {
        Dictionary<string, OutboxCleaner> sequences;
        CancellationTokenSource tokenSource;
        Dictionary<string, string> destinationToInterfaceMap;

        public OutboxCleanerCollection(DeduplicationSettings settings, OutboxPersister persister)
        {
            sequences = settings.GetAllDestinations().ToDictionary(d => d, d => new OutboxCleaner(d, persister, settings.ConnFactory));
            destinationToInterfaceMap = settings.GetAllDestinations().ToDictionary(d => d, d => settings.GetDestinationInterface(d));
        }

        public void ValidateSequence(CapturedTransportOperation operation)
        {
            sequences[operation.Destination].ValidateSequence(operation);
        }

        public LinkState GetLinkState(string destinationKey)
        {
            return sequences[destinationKey].GetLinkState();
        }

        public Task Start(RootContext rootContext)
        {
            tokenSource = new CancellationTokenSource();

            foreach (var sequence in sequences)
            {
                sequence.Value.Start(tokenSource.Token, async operation =>
                {
                    var destinationEndpoint = sequence.Key;
                    var iface = destinationToInterfaceMap[destinationEndpoint];

                    var chains = rootContext.Interfaces.GetChainsFor(iface);
                    var chain = chains.Get<AnycastContext>();
                    var dispatchContext = new OutboxDispatchContext(rootContext, iface);
                    var forwardContext = new AnycastContext(destinationEndpoint, operation, DistributionStrategyScope.Send, dispatchContext);
                    dispatchContext.Set(new TransportTransaction());
                    await chain.Invoke(forwardContext).ConfigureAwait(false);
                });
            }
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            tokenSource?.Cancel();
            return Task.WhenAll(sequences.Values.Select(s => s.Stop()));
        }
    }
}