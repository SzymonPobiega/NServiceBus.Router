namespace NServiceBus.Router.Deduplication.Inbox
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Logging;

    class InboxPersister
    {
        static ILog log = LogManager.GetLogger<InboxPersister>();
        readonly string sourceKey;
        string destinationSequenceKey;
        LinkStateTable linkStateTable;
        volatile LinkState linkState;

        public InboxPersister(string sourceKey, string destinationKey)
        {
            linkStateTable = new LinkStateTable(destinationKey);
            this.sourceKey = sourceKey;
            destinationSequenceKey = destinationKey;
        }

        public async Task<DeduplicationResult> Deduplicate(string messageId, long seq, SqlConnection conn, SqlTransaction trans)
        {
            var cachedLinkState = linkState;

            var tableName = cachedLinkState.GetTableName(seq);
            var table = new InboxTable(tableName);
            try
            {
                await table.Insert(messageId, seq, conn, trans);
                return DeduplicationResult.OK;
            }
            catch (SqlException e)
            {
                if (e.Number == 547)
                {
                    if (seq < cachedLinkState.TailSession.Lo)
                    {
                        return DeduplicationResult.Duplicate;
                    }

                    var freshLinkState = await linkStateTable.Get(sourceKey, conn).ConfigureAwait(false);
                    if (seq < freshLinkState.TailSession.Lo)
                    {
                        return DeduplicationResult.Duplicate;
                    }

                    UpdateCachedLinkState(freshLinkState);
                    throw new ProcessCurrentMessageLaterException("Link state is stale. Processing current message later.");
                }

                if (e.Number == 2627)
                {
                    return DeduplicationResult.Duplicate;
                }

                throw;
            }
        }

        void UpdateCachedLinkState(LinkState freshLinkState)
        {
            lock (this)
            {
                if (freshLinkState.Epoch > linkState.Epoch)
                {
                    linkState = freshLinkState;
                }
            }
        }

        public async Task Prepare(SqlConnection conn)
        {
            linkState = await linkStateTable.Get(sourceKey, conn);
        }

        public async Task Initialize(long headLo, long headHi, long tailLo, long tailHi, SqlConnection conn)
        {
            if (linkState.Initialized)
            {
                return; //We ignore duplicate initialize messages.
            }

            var firstTable = $"Inbox_{sourceKey}_{destinationSequenceKey}_1";
            var secondTable = $"Inbox_{sourceKey}_{destinationSequenceKey}_2";

            var initialized = linkState.Initialize(firstTable, headLo, headHi, secondTable, tailLo, tailHi);

            await linkStateTable.Update(sourceKey, initialized, conn, null).ConfigureAwait(false);

            UpdateCachedLinkState(initialized);
        }

        public async Task Advance(int nextEpoch, long nextLo, long nextHi, SqlConnection conn)
        {
            //Let's actually check if our values are correct.
            var queriedLinkState = await linkStateTable.Get(sourceKey, conn);

            if (!queriedLinkState.Initialized)
            {
                throw new ProcessCurrentMessageLaterException($"Link state for {sourceKey} is not yet initialized. Cannot advance the epoch.");
            }

            var tableName = queriedLinkState.TailSession.Table;
            var table = new InboxTable(tableName);

            if (await table.HasHoles(queriedLinkState.TailSession, conn).ConfigureAwait(false))
            {
                throw new ProcessCurrentMessageLaterException($"Inbox table {tableName} seems to have holes in the sequence. Cannot close yet.");
            }

            log.Debug($"Closing inbox table {tableName}.");
            LinkState newLinkState;
            using (var closeTransaction = conn.BeginTransaction())
            {
                //Ensure only one process can enter here
                var lockedLinkState = await linkStateTable.Lock(sourceKey, conn, closeTransaction);
                if (lockedLinkState.Epoch != queriedLinkState.Epoch)
                {
                    throw new ProcessCurrentMessageLaterException($"Link state for {sourceKey} does not match previously read value. Cannot advance the epoch.");
                }

                if (nextEpoch != lockedLinkState.Epoch + 1)
                {
                    throw new ProcessCurrentMessageLaterException($"The link state is at epoch {lockedLinkState.Epoch} and is not ready to transition to epoch {nextEpoch}");
                }

                newLinkState = lockedLinkState.Advance(nextLo, nextHi);

                await table.CreateConstraint(newLinkState.HeadSession.Lo, newLinkState.HeadSession.Hi, conn, closeTransaction);

                //Here we have all holes plugged and no possibility of inserting new rows. We can truncate
                log.Debug($"Truncating table {tableName}.");
                await table.Truncate(conn, closeTransaction).ConfigureAwait(false);

                await table.DropConstraint(lockedLinkState.HeadSession.Lo, lockedLinkState.HeadSession.Hi, conn, closeTransaction).ConfigureAwait(false);

                log.Debug($"Updating link state for {sourceKey} to {newLinkState}.");

                await linkStateTable.Update(sourceKey, newLinkState, conn, closeTransaction).ConfigureAwait(false);

                closeTransaction.Commit();

            }
            UpdateCachedLinkState(newLinkState);
        }
    }
}
