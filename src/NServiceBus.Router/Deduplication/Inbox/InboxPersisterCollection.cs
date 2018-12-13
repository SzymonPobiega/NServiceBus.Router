namespace NServiceBus.Router.Deduplication.Inbox
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;

    class InboxPersisterCollection : IModule
    {
        Dictionary<string, InboxPersister> persisters;
        Func<SqlConnection> connectionFactory;

        public InboxPersisterCollection(string destinationKey, DeduplicationSettings settings)
        {
            persisters = settings.GetAllSources().ToDictionary(s => s, s => new InboxPersister(s, destinationKey));
            connectionFactory = settings.ConnFactory;
        }

        public Task Initialize(string sourceKey, long headLo, long headHi, long tailLo, long tailHi, SqlConnection conn)
        {
            return persisters[sourceKey].Initialize(headLo, headHi, tailLo, tailHi, conn);
        }

        public Task<DeduplicationResult> Deduplicate(string sourceKey, string messageId, long seq, SqlConnection conn, SqlTransaction trans)
        {
            return persisters[sourceKey].Deduplicate(messageId, seq, conn, trans);
        }

        public Task Advance(string sourceKey, int nextEpoch, long nextLo, long nextHi, SqlConnection conn)
        {
            return persisters[sourceKey].Advance(nextEpoch, nextLo, nextHi, conn);
        }

        public async Task Start(RootContext rootContext)
        {
            using (var conn = connectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                foreach (var persister in persisters.Values)
                {
                    await persister.Prepare(conn).ConfigureAwait(false);
                }
            }
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }
    }
}