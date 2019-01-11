namespace NServiceBus.Router.Deduplication.Inbox
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Logging;

    class InboxPersister
    {
        static ILog log = LogManager.GetLogger<InboxPersister>();
        readonly string sourceKey;
        string destinationKey;
        Func<SqlConnection> connectionFactory;
        LinkStateTable linkStateTable;
        volatile LinkState linkState;

        public InboxPersister(string sourceKey, string destinationKey, Func<SqlConnection> connectionFactory)
        {
            linkStateTable = new LinkStateTable(destinationKey);
            this.sourceKey = sourceKey;
            this.destinationKey = destinationKey;
            this.connectionFactory = connectionFactory;
        }

        public async Task<DeduplicationResult> Deduplicate(string messageId, long seq, SqlConnection conn, SqlTransaction trans)
        {
            var cachedLinkState = linkState;

            if (cachedLinkState.IsDuplicate(seq))
            {
                return DeduplicationResult.Duplicate;
            }

            if (cachedLinkState.IsFromNextEpoch(seq))
            {
                var freshLinkState = await linkStateTable.Get(sourceKey, conn, trans).ConfigureAwait(false);
                UpdateCachedLinkState(freshLinkState);
                cachedLinkState = linkState;
            }

            if (cachedLinkState.IsFromNextEpoch(seq))
            {
                throw new ProcessCurrentMessageLaterException("The message requires advancing epoch. Moving it to the back of the queue.");
            }
            if (cachedLinkState.IsDuplicate(seq))
            {
                return DeduplicationResult.Duplicate;
            }

            var tableName = cachedLinkState.GetTableName(seq);
            var table = new InboxTable(tableName);
            try
            {
                await table.Insert(messageId, seq, conn, trans);
                return DeduplicationResult.OK;
            }
            catch (SqlException e)
            {
                if (e.Number == 547) //Constraint violation
                {
                    var freshLinkState = await linkStateTable.Get(sourceKey, conn, trans).ConfigureAwait(false);
                    UpdateCachedLinkState(freshLinkState);
                    if (freshLinkState.IsDuplicate(seq))
                    {
                        return DeduplicationResult.Duplicate;
                    }
                    throw new ProcessCurrentMessageLaterException("Link state is stale. Processing current message later.");
                }

                if (e.Number == 2627) //Unique index violation
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

        public async Task Prepare()
        {
            using (var conn = connectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                linkState = await linkStateTable.Get(sourceKey, conn, null).ConfigureAwait(false);
            }
        }

        public async Task Initialize(long headLo, long headHi, long tailLo, long tailHi, SqlConnection conn)
        {
            if (linkState.Initialized)
            {
                return; //We ignore duplicate initialize messages.
            }

            LinkState initialized;
            using (var initTransaction = conn.BeginTransaction())
            {
                initialized = linkState.Initialize(
                    InboxTable.Left(sourceKey, destinationKey), headLo, headHi, 
                    InboxTable.Right(sourceKey, destinationKey), tailLo, tailHi);

                await initialized.HeadSession.CreateConstraint(conn, initTransaction).ConfigureAwait(false);
                await initialized.TailSession.CreateConstraint(conn, initTransaction).ConfigureAwait(false);

                await linkStateTable.Update(sourceKey, initialized, conn, initTransaction).ConfigureAwait(false);
                initTransaction.Commit();
            }

            UpdateCachedLinkState(initialized);
        }

        public async Task<LinkState> Advance(long nextEpoch, long nextLo, long nextHi, SqlConnection conn)
        {
            //Let's actually check if our values are correct.
            var queriedLinkState = await linkStateTable.Get(sourceKey, conn, null);

            if (!queriedLinkState.Initialized)
            {
                throw new ProcessCurrentMessageLaterException($"Link state for {sourceKey} is not yet initialized. Cannot advance the epoch.");
            }

            var tableName = queriedLinkState.TailSession.Table;
            var table = new InboxTable(tableName);

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

                if (nextEpoch < lockedLinkState.Epoch + 1)
                {
                    //This is an old message that we already processed.
                    return lockedLinkState;
                }

                if (nextEpoch > lockedLinkState.Epoch + 1)
                {
                    throw new ProcessCurrentMessageLaterException($"The link state is at epoch {lockedLinkState.Epoch} and is not ready to transition to epoch {nextEpoch}.");
                }

                if (await table.HasHoles(queriedLinkState.TailSession, conn, closeTransaction).ConfigureAwait(false))
                {
                    throw new ProcessCurrentMessageLaterException($"Inbox table {tableName} seems to have holes in the sequence. Cannot close yet.");
                }

                newLinkState = lockedLinkState.Advance(nextLo, nextHi);

                await newLinkState.HeadSession.CreateConstraint(conn, closeTransaction);

                //Here we have all holes plugged and no possibility of inserting new rows. We can truncate
                log.Debug($"Truncating table {tableName}.");
                await table.Truncate(conn, closeTransaction).ConfigureAwait(false);

                await lockedLinkState.TailSession.DropConstraint(conn, closeTransaction).ConfigureAwait(false);

                log.Debug($"Updating link state for {sourceKey} to {newLinkState}.");

                await linkStateTable.Update(sourceKey, newLinkState, conn, closeTransaction).ConfigureAwait(false);

                closeTransaction.Commit();

            }
            UpdateCachedLinkState(newLinkState);
            return newLinkState;
        }
    }
}
