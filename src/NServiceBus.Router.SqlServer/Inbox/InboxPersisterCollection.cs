namespace NServiceBus.Router.Deduplication.Inbox
{
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using Settings;

    class InboxPersisterCollection : IModule
    {
        Dictionary<string, InboxPersister> persisters;

        public InboxPersisterCollection(string destinationKey, DeduplicationSettings settings)
        {
            persisters = settings.Links.ToDictionary(x => x.Key, x => new InboxPersister(x.Key, destinationKey, x.Value.ConnectionFactory));
        }

        public Task Initialize(string sourceKey, long headLo, long headHi, long tailLo, long tailHi, SqlConnection conn)
        {
            return persisters[sourceKey].Initialize(headLo, headHi, tailLo, tailHi, conn);
        }

        public Task<DeduplicationResult> Deduplicate(string sourceKey, string messageId, long seq, SqlConnection conn, SqlTransaction trans)
        {
            return persisters[sourceKey].Deduplicate(messageId, seq, conn, trans);
        }

        public Task Advance(string sourceKey, long nextEpoch, long nextLo, long nextHi, SqlConnection conn)
        {
            return persisters[sourceKey].Advance(nextEpoch, nextLo, nextHi, conn);
        }

        public async Task Start(RootContext rootContext, SettingsHolder extensibilitySettings)
        {
            foreach (var persister in persisters.Values)
            {
                await persister.Prepare().ConfigureAwait(false);
            }
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }
    }
}