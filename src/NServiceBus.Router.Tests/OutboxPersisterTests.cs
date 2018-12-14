using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Router;
using NServiceBus.Router.Deduplication.Outbox;
using NServiceBus.Transport;
using NUnit.Framework;

[TestFixture]
public class OutboxPersisterTests
{
    OutboxPersister persister;
    OutboxInstaller installer;

    [SetUp]
    public async Task PrepareTables()
    {
        LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

        installer = new OutboxInstaller("S");
        persister = new OutboxPersister(3, "S", "D");
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            using (var trans = conn.BeginTransaction())
            {
                await installer.Uninstall("D", conn, trans).ConfigureAwait(false);
                await installer.Install("D", conn, trans).ConfigureAwait(false);
                trans.Commit();
            }
        }
    }

    static SqlConnection CreateConnection()
    {
        return new SqlConnection("data source = (local); initial catalog=test1; integrated security=true");
    }

    [Test]
    public async Task It_stores_messages_in_tables_based_on_sequence_number()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            using (var trans = conn.BeginTransaction())
            {
                for (var i = 1; i <= 6; i++)
                {
                    var operation = CreateMessages(Guid.NewGuid().ToString());
                    await persister.Store(operation, () => {}, conn, trans).ConfigureAwait(false);
                }

                trans.Commit();
            }
        }
    }

    static CapturedTransportOperation CreateMessages(string messageId)
    {
        var outgoingMessage = new OutgoingMessage(messageId, new Dictionary<string, string>(), new byte[3]);
        return new CapturedTransportOperation(outgoingMessage, "D");
    }

    [Test]
    public async Task Can_advance_without_sending()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            Task Dispatch(OutgoingMessage operation)
            {
                Console.WriteLine(operation.MessageId);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);
            await persister.TryAdvance(Dispatch, conn);
            await persister.TryAdvance(Dispatch, conn);
            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(4, linkState.Epoch);
        }
    }

    [Test]
    public async Task Can_close_non_empty_table()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            Task Dispatch(OutgoingMessage operation)
            {
                Console.WriteLine(operation.MessageId);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            await persister.Store(CreateMessages("M1"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M2"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M3"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M4"), () => { }, conn, null).ConfigureAwait(false);

            var linkState = await persister.TryAdvance(Dispatch, conn);

            await persister.Store(CreateMessages("M5"), () => { }, conn, null).ConfigureAwait(false);

            Assert.AreEqual(6, linkState.HeadSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);
        }
    }

    [Test]
    public async Task It_triggers_advance_when_stale_or_full()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            Task Dispatch(OutgoingMessage operation)
            {
                Console.WriteLine(operation.MessageId);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            await persister.Store(CreateMessages("M1"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M2"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M3"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M4"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M5"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M6"), () => { }, conn, null).ConfigureAwait(false);

            var advanceTriggered = false;
            try
            {
                await persister.Store(CreateMessages("M7"), () => { advanceTriggered = true; }, conn, null).ConfigureAwait(false);
            }
            catch (ProcessCurrentMessageLaterException)
            {
                //Swallow
            }

            Assert.IsTrue(advanceTriggered);
        }
    }

    [Test]
    public async Task It_triggers_advance_when_head_table_is_half_full()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            Task Dispatch(OutgoingMessage operation)
            {
                Console.WriteLine(operation.MessageId);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            var advanceTriggered = false;
            void TriggerAdvance()
            {
                advanceTriggered = true;
            }

            await persister.Store(CreateMessages("M1"), TriggerAdvance, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M2"), TriggerAdvance, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M3"), TriggerAdvance, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M4"), TriggerAdvance, conn, null).ConfigureAwait(false);

            //Not yet
            Assert.IsFalse(advanceTriggered);

            await persister.Store(CreateMessages("M5"), TriggerAdvance, conn, null).ConfigureAwait(false);

            //Now
            Assert.IsTrue(advanceTriggered);
        }
    }

    [Test]
    public async Task It_detects_a_gap_when_it_is_in_the_middle_of_the_epoch()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();
            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);
            void TriggerAdvance()
            {
                //NOOP
            }

            await persister.Store(CreateMessages("M1"), TriggerAdvance, conn, null).ConfigureAwait(false);
            await GetNextSequenceValue("S_D", conn, null);
            await persister.Store(CreateMessages("M3"), TriggerAdvance, conn, null).ConfigureAwait(false);

            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(3, linkState.TailSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);

            var plug = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Plug));
            Assert.AreEqual("1", plug.Headers[RouterHeaders.SequenceNumber]);
        }
    }

    [Test]
    public async Task It_detects_a_gap_when_it_is_bigger_than_one_row()
    {

        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();

            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);
            await persister.Store(CreateMessages("M1"), () => { }, conn, null);
            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(3, linkState.TailSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);

            var plugsSequenceNumbers = dispatchedMessages
                .Where(m => m.Headers.ContainsKey(RouterHeaders.Plug))
                .Select(m => m.Headers[RouterHeaders.SequenceNumber])
                .ToArray();

            CollectionAssert.AreEqual(new[] { "1", "2" }, plugsSequenceNumbers);
        }
    }

    [Test]
    public async Task It_detects_a_gap_when_it_is_at_the_beginning_of_the_epoch()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();
            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            await GetNextSequenceValue("S_D", conn, null);

            await persister.Store(CreateMessages("M2"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M3"), () => { }, conn, null).ConfigureAwait(false);

            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(3, linkState.TailSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);

            var plug = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Plug));
            Assert.AreEqual("0", plug.Headers[RouterHeaders.SequenceNumber]);
        }
    }

    [Test]
    public async Task It_detects_a_gap_when_it_is_at_the_end_of_the_epoch()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();
            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            await persister.Store(CreateMessages("M1"), () => { }, conn, null).ConfigureAwait(false);
            await persister.Store(CreateMessages("M2"), () => { }, conn, null).ConfigureAwait(false);

            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(3, linkState.TailSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);

            var plug = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Plug));
            Assert.AreEqual("2", plug.Headers[RouterHeaders.SequenceNumber]);
        }
    }

    [Test]
    public async Task It_detects_a_gap_when_the_epoch_is_empty()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();
            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(3, linkState.TailSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);

            var plugsSequenceNumbers = dispatchedMessages
                .Where(m => m.Headers.ContainsKey(RouterHeaders.Plug))
                .Select(m => m.Headers[RouterHeaders.SequenceNumber])
                .ToArray();

            CollectionAssert.AreEqual(new[] { "0", "1", "2" }, plugsSequenceNumbers);
        }
    }

    [Test]
    public async Task It_sends_announce_message_when_advancing()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();
            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(6, linkState.HeadSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);

            var advanceMessage = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Advance));

            Assert.AreEqual("2", advanceMessage.Headers[RouterHeaders.AdvanceEpoch]);
            Assert.AreEqual("6", advanceMessage.Headers[RouterHeaders.AdvanceHeadLo]);
            Assert.AreEqual("9", advanceMessage.Headers[RouterHeaders.AdvanceHeadHi]);
        }
    }

    [Test]
    public async Task It_retries_announcing_advance()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();
            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            var announceFailed = false;
            try
            {
                await persister.TryAdvance(op =>
                {
                    if (op.Headers.ContainsKey(RouterHeaders.Advance))
                    {
                        throw new Exception("Simulated");
                    }
                    return Dispatch(op);
                }, conn);
            }
            catch (Exception)
            {
                announceFailed = true;
            }

            Assert.IsTrue(announceFailed);

            var linkState = await persister.TryAdvance(Dispatch, conn);

            Assert.AreEqual(6, linkState.HeadSession.Lo);
            Assert.AreEqual(9, linkState.HeadSession.Hi);

            var advanceMessage = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Advance));

            Assert.AreEqual("2", advanceMessage.Headers[RouterHeaders.AdvanceEpoch]);
            Assert.AreEqual("6", advanceMessage.Headers[RouterHeaders.AdvanceHeadLo]);
            Assert.AreEqual("9", advanceMessage.Headers[RouterHeaders.AdvanceHeadHi]);
        }
    }

    [Test]
    public async Task It_retries_announcing_advance_when_initializing()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var dispatchedMessages = new List<OutgoingMessage>();
            Task Dispatch(OutgoingMessage operation)
            {
                dispatchedMessages.Add(operation);
                return Task.CompletedTask;
            }

            await persister.Initialize(Dispatch, conn);

            var announceFailed = false;
            try
            {
                await persister.TryAdvance(op =>
                {
                    if (op.Headers.ContainsKey(RouterHeaders.Advance))
                    {
                        throw new Exception("Simulated");
                    }
                    return Dispatch(op);
                }, conn);
            }
            catch (Exception)
            {
                announceFailed = true;
            }

            Assert.IsTrue(announceFailed);

            var newPersister = new OutboxPersister(3, "S", "D");
            await newPersister.Initialize(Dispatch, conn);

            var advanceMessage = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Advance));

            Assert.AreEqual("2", advanceMessage.Headers[RouterHeaders.AdvanceEpoch]);
            Assert.AreEqual("6", advanceMessage.Headers[RouterHeaders.AdvanceHeadLo]);
            Assert.AreEqual("9", advanceMessage.Headers[RouterHeaders.AdvanceHeadHi]);
        }
    }

    async Task<long> GetNextSequenceValue(string sequenceKey, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($"select next value for [Sequence_{sequenceKey}]", conn, trans))
        {
            var value = (long)await command.ExecuteScalarAsync().ConfigureAwait(false);
            return value;
        }
    }
}
