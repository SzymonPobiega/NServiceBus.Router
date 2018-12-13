using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Transport;

namespace NServiceBus.Router.Deduplication
{
    class OutboxPersister
    {
        int epochSize;
        string sourceKey;
        static ILog log = LogManager.GetLogger<OutboxPersister>();
        LinkStateTable linkStateTable;

        public OutboxPersister(int epochSize, string sourceKey)
        {
            linkStateTable = new LinkStateTable(sourceKey);
            this.epochSize = epochSize;
            this.sourceKey = sourceKey;
        }

        public async Task Install(string destinationKey, SqlConnection conn, SqlTransaction trans)
        {
            var firstTable = new OutboxTable($"Outbox_{sourceKey}_{destinationKey}_1");
            var secondTable = new OutboxTable($"Outbox_{sourceKey}_{destinationKey}_2");
            var sequence = new OutboxSequence(sourceKey, destinationKey);

            await linkStateTable.Create(conn, trans).ConfigureAwait(false);

            await firstTable.Create(conn, trans).ConfigureAwait(false);
            await firstTable.CreateConstraint(0, 0, conn, trans).ConfigureAwait(false);
            await secondTable.Create(conn, trans).ConfigureAwait(false);
            await secondTable.CreateConstraint(0, 0, conn, trans).ConfigureAwait(false);

            await sequence.Create(conn, trans).ConfigureAwait(false);

            await linkStateTable.InitializeLink(destinationKey, conn, trans);
        }

        public async Task Uninstall(string destinationKey, SqlConnection conn, SqlTransaction trans)
        {
            var firstTable = new OutboxTable($"Outbox_{sourceKey}_{destinationKey}_1");
            var secondTable = new OutboxTable($"Outbox_{sourceKey}_{destinationKey}_2");
            var sequence = new OutboxSequence(sourceKey, destinationKey);

            await linkStateTable.Drop(conn, trans).ConfigureAwait(false);

            await firstTable.Drop(conn, trans).ConfigureAwait(false);
            await secondTable.Drop(conn, trans).ConfigureAwait(false);

            await sequence.Drop(conn, trans).ConfigureAwait(false);
        }

        public async Task Store(List<CapturedTransportOperation> capturedMessages, Action<CapturedTransportOperation> validateSequence, SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var op in capturedMessages)
            {
                await Store(op, validateSequence, connection, transaction);
            }
        }

        async Task Store(CapturedTransportOperation operation, Action<CapturedTransportOperation> validateSequence, SqlConnection conn, SqlTransaction trans)
        {
            var sequence = new OutboxSequence(sourceKey, operation.Destination);
            var table = new OutboxTable(operation.Table);
            var seq = await sequence.GetNextValue(conn, trans).ConfigureAwait(false);
            try
            {
                var message = Convert(operation.OutgoingMessage);

                operation.OutgoingMessage.Headers[RouterHeaders.SequenceNumber] = seq.ToString();
                operation.OutgoingMessage.Headers[RouterHeaders.SequenceKey] = sourceKey;

                operation.AssignSequence(seq);
                validateSequence(operation);

                await table.Insert(message, seq, conn, trans).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Debug($"Unhandled exception while storing outbox operation with sequence {seq}", ex);
                throw;
            }
        }

        public async Task<LinkState> Initialize(string destinationKey, Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            var linkState = await linkStateTable.Get(destinationKey, conn);
            if (linkState.Initialized && linkState.IsEpochAnnounced)
            {
                return linkState;
            }

            var initializedState = await InitializeLinkStateInDatabase(destinationKey, conn);

            var (announcedState, initializeMessage) = initializedState.AnnounceInitialize(sourceKey);

            log.Debug($"Sending initialize message for {destinationKey}.");
            await dispatch(initializeMessage).ConfigureAwait(false);

            await linkStateTable.Update(destinationKey, announcedState, conn, null).ConfigureAwait(false);

            log.Info($"Link {destinationKey} initialized.");

            return announcedState;
        }

        async Task<LinkState> InitializeLinkStateInDatabase(string destinationKey, SqlConnection conn)
        {
            log.Debug($"Initializing outbox table for {destinationKey}.");
            using (var initTransaction = conn.BeginTransaction())
            {
                //Ensure only one process can enter here
                var lockedLinkState = await linkStateTable.Lock(destinationKey, conn, initTransaction);
                if (lockedLinkState.Initialized)
                {
                    return lockedLinkState;
                }
                var head = $"Outbox_{sourceKey}_{destinationKey}_1";
                var tail = $"Outbox_{sourceKey}_{destinationKey}_2";

                await lockedLinkState.HeadSession.CreateConstraint(conn, initTransaction);

                lockedLinkState = lockedLinkState.Initialize(head, tail, epochSize);

                await linkStateTable.Update(destinationKey, lockedLinkState, conn, initTransaction).ConfigureAwait(false);

                return lockedLinkState;
            }
        }

        public async Task<LinkState> TryClose(string destinationKey, LinkState previousState,
            Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
        {
            //Let's actually check if our values are correct.
            var linkState = await linkStateTable.Get(destinationKey, conn);

            if (!linkState.IsEpochAnnounced)
            {
                return await AnnounceAdvance(destinationKey, linkState, dispatch, conn).ConfigureAwait(false);
            }

            if (previousState.Epoch != linkState.Epoch) //The epoch changed. Please re-evaluate if we need to close.
            {
                log.Debug($"Link state for {destinationKey} does not match cached value. Cannot advance the epoch.");
                return linkState;
            }

            var tableName = linkState.TailSession.Table;
            var table = new OutboxTable(tableName);

            if (await table.HasHoles(linkState.TailSession, conn).ConfigureAwait(false))
            {
                var holes = await table.FindHoles(linkState.TailSession, conn).ConfigureAwait(false);
                if (holes.Any())
                {
                    await PlugHoles(table, holes, dispatch, conn).ConfigureAwait(false);
                }
            }

            var (advanced, newState) = await TryAdvanceEpochInDatabase(destinationKey, conn, tableName, linkState);
            return advanced 
                ? await AnnounceAdvance(destinationKey, newState, dispatch, conn).ConfigureAwait(false) 
                : newState;
        }

        async Task<LinkState> AnnounceAdvance(string destinationKey, LinkState newState, Func<OutgoingMessage, Task> dispatch, SqlConnection conn)
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

        async Task<(bool, LinkState)> TryAdvanceEpochInDatabase(string destinationKey, SqlConnection conn, string tableName, LinkState queriedLinkState)
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

                await table.CreateConstraint(newState.HeadSession.Lo, newState.HeadSession.Hi, conn, advanceTransaction);

                //Here we have all holes plugged and no possibility of inserting new rows. We can truncate
                log.Debug($"Truncating table {tableName}.");
                await table.Truncate(conn, advanceTransaction).ConfigureAwait(false);

                await table.DropConstraint(newState.TailSession.Lo, newState.TailSession.Hi, conn, advanceTransaction).ConfigureAwait(false);

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
                [RouterHeaders.SequenceNumber] = seq.ToString(),
                [RouterHeaders.SequenceKey] = sourceKey,
                [RouterHeaders.Plug] = "true"
            };
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, new byte[0]);
            return message;
        }

        public static Task MarkAsDispatched(CapturedTransportOperation operation, SqlConnection conn, SqlTransaction trans)
        {
            return new OutboxTable(operation.Table).MarkAsDispatched(operation.Sequence, conn, trans);
        }

        static PersistentOutboxTransportOperation Convert(OutgoingMessage message)
        {
            var persistentOp = new PersistentOutboxTransportOperation(message.MessageId, message.Body, message.Headers);
            return persistentOp;
        }
    }

    enum HoleType
    {
        MissingRow,
        UndispatchedRow
    }
}
