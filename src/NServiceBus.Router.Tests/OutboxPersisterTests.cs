//using System;
//using System.Collections.Generic;
//using System.Data.SqlClient;
//using System.Linq;
//using System.Threading.Tasks;
//using NServiceBus.Logging;
//using NServiceBus.Router.Deduplication;
//using NServiceBus.Transport;
//using NUnit.Framework;

//[TestFixture]
//public class OutboxPersisterTests
//{
//    OutboxPersister persister;

//    [SetUp]
//    public async Task PrepareTables()
//    {
//        LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

//        persister = new OutboxPersister(3, "S");
//        using (var conn = CreateConnection())
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            using (var trans = conn.BeginTransaction())
//            {
//                await persister.Install("A", conn, trans).ConfigureAwait(false);
//                trans.Commit();
//            }
//        }
//    }

//    static SqlConnection CreateConnection()
//    {
//        return new SqlConnection("data source = (local); initial catalog=test1; integrated security=true");
//    }

//    [Test]
//    public async Task It_stores_messages_in_tables_based_on_sequence_number()
//    {

//        using (var conn = CreateConnection())
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            using (var trans = conn.BeginTransaction())
//            {
//                for (var i = 1; i <= 6; i++)
//                {
//                    var messages = CreateMessages(Guid.NewGuid().ToString());
//                    await persister.Store(messages, op => { }, conn, trans).ConfigureAwait(false);
//                }

//                trans.Commit();
//            }
//        }
//    }

//    static List<CapturedTransportOperation> CreateMessages(string messageId)
//    {
//        var outgoingMessage = new OutgoingMessage(messageId, new Dictionary<string, string>(), new byte[3]);
//        var messages = new List<CapturedTransportOperation>
//        {
//            new CapturedTransportOperation(outgoingMessage, "A")
//        };
//        return messages;
//    }

//    [Test]
//    [Explicit]
//    public async Task Cannot_close_empty_epoch()
//    {
//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);
//            var prevState = new LinkState(0, new SessionState(0, 0, null), new SessionState(0, 0, null));
//            var linkState = await persister.TryClose("A", prevState, operation =>
//            {
//                Console.WriteLine(operation.MessageId);
//                return Task.CompletedTask;
//            }, conn);

//            Assert.AreEqual(0, linkState.);
//            Assert.AreEqual(6, hi);
//        }
//    }

//    [Test]
//    public async Task Can_close_non_empty_table()
//    {
//        var sequences = new Dictionary<string, long>();

//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            await persister.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M2"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M4"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            var (lo, hi) = await persister.TryClose("A", 0, 6, operation =>
//            {
//                Console.WriteLine(operation.MessageId);
//                return Task.CompletedTask;
//            }, conn);

//            await persister.Store(CreateMessages("M5"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            Assert.AreEqual(3, lo);
//            Assert.AreEqual(9, hi);
//        }
//    }

//    [Test]
//    public async Task It_refuses_to_insert_when_full()
//    {
//        var sequences = new Dictionary<string, long>();

//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            await persister.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M2"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M4"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M5"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M6"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            await persister.Store(CreateMessages("M7"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//        }
//    }

//    [Test]
//    public async Task It_detects_a_gap_when_it_is_in_the_middle_of_the_epoch()
//    {
//        var sequences = new Dictionary<string, long>();

//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            await persister.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            await GetNextSequenceValue("S_A", conn, null);

//            await persister.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            var dispatchedMessages = new List<OutgoingMessage>();

//            var (lo, hi) = await persister.TryClose("A", 0, 6, operation =>
//            {
//                dispatchedMessages.Add(operation);
//                return Task.CompletedTask;
//            }, conn);

//            Assert.AreEqual(3, lo);
//            Assert.AreEqual(9, hi);

//            var plug = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Plug));
//            Assert.AreEqual("1", plug.Headers[RouterHeaders.SequenceNumber]);
//        }
//    }

//    [Test]
//    public async Task It_detects_a_gap_when_it_is_bigger_than_one_row()
//    {
//        var sequences = new Dictionary<string, long>();

//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            await persister.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            var dispatchedMessages = new List<OutgoingMessage>();

//            var (lo, hi) = await persister.TryClose("A", 0, 6, operation =>
//            {
//                dispatchedMessages.Add(operation);
//                return Task.CompletedTask;
//            }, conn);

//            Assert.AreEqual(3, lo);
//            Assert.AreEqual(9, hi);

//            var plugsSequenceNumbers = dispatchedMessages
//                .Where(m => m.Headers.ContainsKey(RouterHeaders.Plug))
//                .Select(m => m.Headers[RouterHeaders.SequenceNumber])
//                .ToArray();

//            CollectionAssert.AreEqual(new[] {"1", "2"}, plugsSequenceNumbers );
//        }
//    }

//    [Test]
//    public async Task It_detects_a_gap_when_it_is_at_the_beginning_of_the_epoch()
//    {
//        var sequences = new Dictionary<string, long>();

//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            await GetNextSequenceValue("S_A", conn, null);

//            await persister.Store(CreateMessages("M2"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            var dispatchedMessages = new List<OutgoingMessage>();

//            var (lo, hi) = await persister.TryClose("A", 0, 6, operation =>
//            {
//                dispatchedMessages.Add(operation);
//                return Task.CompletedTask;
//            }, conn);

//            Assert.AreEqual(3, lo);
//            Assert.AreEqual(9, hi);

//            var plug = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Plug));
//            Assert.AreEqual("0", plug.Headers[RouterHeaders.SequenceNumber]);
//        }
//    }

//    [Test]
//    public async Task It_detects_a_gap_when_it_is_at_the_end_of_the_epoch()
//    {
//        var sequences = new Dictionary<string, long>();

//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);


//            await persister.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
//            await persister.Store(CreateMessages("M2"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

//            var dispatchedMessages = new List<OutgoingMessage>();

//            var (lo, hi) = await persister.TryClose("A", 0, 6, operation =>
//            {
//                dispatchedMessages.Add(operation);
//                return Task.CompletedTask;
//            }, conn);

//            Assert.AreEqual(3, lo);
//            Assert.AreEqual(9, hi);

//            var plug = dispatchedMessages.Single(m => m.Headers.ContainsKey(RouterHeaders.Plug));
//            Assert.AreEqual("2", plug.Headers[RouterHeaders.SequenceNumber]);
//        }
//    }

//    [Test]
//    public async Task It_detects_a_gap_when_the_epoch_is_empty()
//    {
//        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
//        {
//            await conn.OpenAsync().ConfigureAwait(false);

//            var dispatchedMessages = new List<OutgoingMessage>();

//            var (lo, hi) = await persister.TryClose("A", 0, 6, operation =>
//            {
//                dispatchedMessages.Add(operation);
//                return Task.CompletedTask;
//            }, conn);

//            Assert.AreEqual(3, lo);
//            Assert.AreEqual(9, hi);

//            var plugsSequenceNumbers = dispatchedMessages
//                .Where(m => m.Headers.ContainsKey(RouterHeaders.Plug))
//                .Select(m => m.Headers[RouterHeaders.SequenceNumber])
//                .ToArray();

//            CollectionAssert.AreEqual(new[] { "0", "1", "2" }, plugsSequenceNumbers);
//        }
//    }

//    async Task<long> GetNextSequenceValue(string sequenceKey, SqlConnection conn, SqlTransaction trans)
//    {
//        using (var command = new SqlCommand($"select next value for [Sequence_{sequenceKey}]", conn, trans))
//        {
//            var value = (long)await command.ExecuteScalarAsync().ConfigureAwait(false);
//            return value;
//        }
//    }
//}
