namespace NServiceBus.Router.Deduplication.Outbox
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Settings;
    using Transport;

    class OutboxPersisterCollection : IModule
    {
        Dictionary<string, OutboxPersisterRunner> persisters;
        CancellationTokenSource tokenSource;
        Dictionary<string, string> destinationToInterfaceMap;

        public OutboxPersisterCollection(string sourceKey, DeduplicationSettings settings)
        {
            persisters = settings.Links.ToDictionary(x => x.Key, 
                x => new OutboxPersisterRunner(new OutboxPersister(x.Value.EpochSize, sourceKey, x.Key), x.Value.ConnectionFactory));
            destinationToInterfaceMap = settings.Links.ToDictionary(x => x.Key, x => x.Value.LinkInterface);
        }

        public Task Store(CapturedTransportOperation operation, SqlConnection conn, SqlTransaction trans)
        {
            return persisters[operation.Destination].Store(operation, conn, trans);
        }

        public async Task Start(RootContext rootContext, SettingsHolder extensibilitySettings)
        {
            tokenSource = new CancellationTokenSource();

            foreach (var persister in persisters)
            {
                async Task Dispatch(OutgoingMessage operation)
                {
                    var destinationEndpoint = persister.Key;
                    var iface = destinationToInterfaceMap[destinationEndpoint];

                    var chains = rootContext.Interfaces.GetChainsFor(iface);
                    var chain = chains.Get<AnycastContext>();
                    var dispatchContext = new OutboxDispatchContext(rootContext, iface);
                    var forwardContext = new AnycastContext(destinationEndpoint, operation, DistributionStrategyScope.Send, dispatchContext);
                    dispatchContext.Set(new TransportTransaction());
                    await chain.Invoke(forwardContext).ConfigureAwait(false);
                }

                await persister.Value.Start(tokenSource.Token, Dispatch);
            }
        }

        public Task Stop()
        {
            tokenSource?.Cancel();
            return Task.WhenAll(persisters.Values.Select(s => s.Stop()));
        }
    }
}