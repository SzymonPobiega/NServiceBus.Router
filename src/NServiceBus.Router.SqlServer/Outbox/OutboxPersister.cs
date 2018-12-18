namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using Logging;
    using Transport;

    class OutboxPersister
    {
        static ILog log = LogManager.GetLogger<OutboxPersister>();

        int epochSize;
        string sourceKey;
        string destinationKey;
        LinkStateTable linkStateTable;
        OutboxSequence sequence;
        LinkState linkState;
        long highestSeq;

        public OutboxPersister(int epochSize, string sourceKey, string destinationKey)
        {
            linkStateTable = new LinkStateTable(sourceKey);
            sequence = new OutboxSequence(sourceKey, destinationKey);
            this.epochSize = epochSize;
            this.sourceKey = sourceKey;
            this.destinationKey = destinationKey;
        }

        public async Task Store(CapturedTransportOperation operation, Action triggerAdvance, SqlConnection conn, SqlTransaction trans)
        {
            var localState = linkState;

            var seq = await sequence.GetNextValue(conn, trans).ConfigureAwait(false);
            InterlocedEx.ExchangeIfGreaterThan(ref highestSeq, seq);

            if (localState.IsStale(seq))
            {
                var freshLinkState = await linkStateTable.Get(operation.Destination, conn, trans).ConfigureAwait(false);
                UpdateCachedLinkState(freshLinkState);
                localState = linkState;
            }

            if (localState.ShouldAdvance(seq))
            {
                triggerAdvance();
            }

            if (localState.IsStale(seq))
            {
                throw new ProcessCurrentMessageLaterException("Link state is stale. Processing current message later.");
            }

            var tableName = localState.GetTableName(seq);
            var table = new OutboxTable(tableName);

            try
            {
                operation.OutgoingMessage.Headers[RouterDeduplicationHeaders.SequenceNumber] = seq.ToString();
                operation.OutgoingMessage.Headers[RouterDeduplicationHeaders.SequenceKey] = sourceKey;
                operation.AssignTable(tableName);
                operation.AssignSequence(seq);

                var persistentOperation = Convert(operation);

                await table.Insert(persistentOperation, seq, conn, trans).ConfigureAwait(false);
            }
            catch (SqlException e)
            {
                if (e.Number == 547) //Constraint violation. We used very old seq and that value cannot be used any more because the epoch has advanced.
                {
                    var freshLinkState = await linkStateTable.Get(operation.Destination, conn, trans).ConfigureAwait(false);
                    UpdateCachedLinkState(freshLinkState);
                    throw new ProcessCurrentMessageLaterException("Link state is stale. Processing current message later.");
                }
                throw;
            }
            catch (Exception ex)
            {
                log.Debug($"Unhandled exception while storing outbox operation with sequence {seq}", ex);
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

        public async Task Initialize(Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            LinkState initializedState;

            using (var initTransaction = conn.BeginTransaction())
            {
                //Ensure only one process can enter here
                var lockedLinkState = await linkStateTable.Lock(destinationKey, conn, initTransaction).ConfigureAwait(false);

                //Initialize if not already initialized
                initializedState = await InitializeLinkState(lockedLinkState, conn, initTransaction).ConfigureAwait(false);

                initTransaction.Commit();
            }

            //Announce if initialized 
            var announcedState = await AnnounceInitializeOrAdvance(initializedState, dispatch, conn).ConfigureAwait(false);
            linkState = announcedState;
        }

        async Task<LinkState> AnnounceInitializeOrAdvance(LinkState initializedState, Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            if (initializedState.IsEpochAnnounced)
            {
                return initializedState;
            }
            return initializedState.Epoch == 1
                ? await AnnounceInitialize(initializedState, dispatch, conn).ConfigureAwait(false)
                : await AnnounceAdvance(initializedState, dispatch, conn).ConfigureAwait(false);
        }

        async Task<LinkState> AnnounceInitialize(LinkState initializedState, Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            var (announcedLinkState, initializeMessage) = initializedState.AnnounceInitialize(sourceKey);

            log.Debug($"Sending initialize message for {destinationKey}.");
            await dispatch(initializeMessage).ConfigureAwait(false);

            await linkStateTable.Update(destinationKey, announcedLinkState, conn, null).ConfigureAwait(false);

            return announcedLinkState;
        }

        async Task<LinkState> InitializeLinkState(LinkState lockedLinkState, SqlConnection conn, SqlTransaction initTransaction)
        {
            if (!lockedLinkState.Initialized)
            {
                var initializedState = lockedLinkState.Initialize(
                    OutboxTable.Left(sourceKey, destinationKey),
                    OutboxTable.Right(sourceKey, destinationKey), epochSize);

                await initializedState.HeadSession.CreateConstraint(conn, initTransaction);
                await initializedState.TailSession.CreateConstraint(conn, initTransaction);

                await linkStateTable.Update(destinationKey, initializedState, conn, initTransaction).ConfigureAwait(false);
                return initializedState;
            }

            return lockedLinkState;
        }

        public async Task<LinkState> TryAdvance(Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            var newState = await Advance(dispatch, conn).ConfigureAwait(false);
            UpdateCachedLinkState(newState);
            return newState;
        }

        async Task<LinkState> Advance(Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            //Let's actually check if our values are correct.
            var queriedLinkState = await linkStateTable.Get(destinationKey, conn);

            if (!queriedLinkState.ShouldAdvance(highestSeq))
            {
                return queriedLinkState;
            }

            log.Debug($"Attempting advance epoch for destination {destinationKey} based on link state {linkState}.");

            if (!queriedLinkState.IsEpochAnnounced)
            {
                return await AnnounceAdvance(queriedLinkState, dispatch, conn).ConfigureAwait(false);
            }

            var tableName = queriedLinkState.TailSession.Table;
            var table = new OutboxTable(tableName);

            if (await table.HasHoles(queriedLinkState.TailSession, conn).ConfigureAwait(false))
            {
                var holes = await table.FindHoles(queriedLinkState.TailSession, conn).ConfigureAwait(false);
                if (holes.Any())
                {
                    await PlugHoles(table, holes, dispatch, conn).ConfigureAwait(false);
                }
            }

            var (advanced, newState) = await TryAdvanceEpochInDatabase(conn, tableName, queriedLinkState);
            if (advanced)
            {
                return await AnnounceAdvance(newState, dispatch, conn).ConfigureAwait(false);
            }
            return newState;
        }

        async Task<LinkState> AnnounceAdvance(LinkState newState, Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            var (announcedState, announceMessage) = newState.AnnounceAdvance(sourceKey);

            log.Debug($"Sending advance message for {destinationKey}.");
            await dispatch(announceMessage).ConfigureAwait(false);

            await linkStateTable.Update(destinationKey, announcedState, conn, null).ConfigureAwait(false);

            log.Info($"Link {destinationKey} epoch advance announced.");

            return announcedState;
        }

        async Task PlugHoles(OutboxTable table, List<(long Id, HoleType Type)> holes, Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            log.Debug($"Outbox table {table.Name} seems to have holes in the sequence. Attempting to plug them.");

            //Plug missing row holes by inserting dummy rows
            foreach (var hole in holes.Where(h => h.Type == HoleType.MissingRow))
            {
                //If we blow here it means that some other process inserted rows after we looked for holes. We backtrack and come back
                log.Debug($"Plugging hole {hole.Id} with a dummy message row.");
                await table.PlugHole(hole.Id, conn).ConfigureAwait(false);
            }

            //Dispatch all the holes and mark them as dispatched
            foreach (var hole in holes)
            {
                OutgoingMessage message;
                if (hole.Type == HoleType.MissingRow)
                {
                    message = CreatePlugMessage(hole.Id);
                    log.Debug($"Dispatching dummy message row {hole.Id}.");
                }
                else
                {
                    message = await table.LoadMessageById(hole.Id, conn).ConfigureAwait(false);
                    log.Debug($"Dispatching message {hole.Id} with ID {message.MessageId}.");
                }

                await dispatch(message).ConfigureAwait(false);
                await table.MarkAsDispatched(hole.Id, conn, null).ConfigureAwait(false);
            }
        }

        async Task<(bool, LinkState)> TryAdvanceEpochInDatabase(SqlConnection conn, string tableName, LinkState queriedLinkState)
        {
            log.Debug($"Closing outbox table {tableName}.");
            using (var advanceTransaction = conn.BeginTransaction())
            {
                //Ensure only one process can enter here
                var lockedLinkState = await linkStateTable.Lock(destinationKey, conn, advanceTransaction);
                if (lockedLinkState.Epoch != queriedLinkState.Epoch)
                {
                    log.Debug($"Link state for {destinationKey} does not match previously read value. Cannot advance the epoch.");
                    return (false, lockedLinkState);
                }

                var newState = lockedLinkState.Advance(epochSize);

                var table = new OutboxTable(tableName);

                await newState.HeadSession.CreateConstraint(conn, advanceTransaction).ConfigureAwait(false);

                //Here we have all holes plugged and no possibility of inserting new rows. We can truncate
                log.Debug($"Truncating table {tableName}.");
                await table.Truncate(conn, advanceTransaction).ConfigureAwait(false);

                await lockedLinkState.TailSession.DropConstraint(conn, advanceTransaction).ConfigureAwait(false);

                log.Debug($"Updating state for destinationKey {destinationKey} to {newState}");
                await linkStateTable.Update(destinationKey, newState, conn, advanceTransaction).ConfigureAwait(false);

                advanceTransaction.Commit();

                return (true, newState);
            }
        }

        OutgoingMessage CreatePlugMessage(long seq)
        {
            var headers = new Dictionary<string, string>
            {
                [RouterDeduplicationHeaders.SequenceNumber] = seq.ToString(),
                [RouterDeduplicationHeaders.SequenceKey] = sourceKey,
                [RouterDeduplicationHeaders.Plug] = "true"
            };
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, new byte[0]);
            return message;
        }

        public static Task MarkAsDispatched(CapturedTransportOperation operation, SqlConnection conn, SqlTransaction trans)
        {
            return new OutboxTable(operation.Table).MarkAsDispatched(operation.Sequence, conn, trans);
        }

        static PersistentOutboxTransportOperation Convert(CapturedTransportOperation operation)
        {
            var message = operation.OutgoingMessage;
            var persistentOp = new PersistentOutboxTransportOperation(message.MessageId, message.Body, message.Headers);
            return persistentOp;
        }
    }
}
