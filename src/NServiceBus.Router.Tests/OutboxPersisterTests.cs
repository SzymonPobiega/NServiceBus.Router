using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Router.Deduplication;
using NServiceBus.Transport;
using NUnit.Framework;

[TestFixture]
public class OutboxPersisterTests
{
    OutboxPersistence persistence;

    [SetUp]
    public async Task PrepareTables()
    {
        LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

        persistence = new OutboxPersistence(3, "source");
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            using (var trans = conn.BeginTransaction())
            {
                await persistence.Install("A", conn, trans).ConfigureAwait(false);
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
                var sequences = new Dictionary<string, long>();

                for (var i = 1; i <= 6; i++)
                {
                    var messages = CreateMessages(Guid.NewGuid().ToString());
                    await persistence.Store(messages, (key, value) => { sequences[key] = value; }, conn, trans).ConfigureAwait(false);
                }

                trans.Commit();
            }
        }
    }

    static List<CapturedTransportOperation> CreateMessages(string messageId)
    {
        var outgoingMessage = new OutgoingMessage(messageId, new Dictionary<string, string>(), new byte[3]);
        var messages = new List<CapturedTransportOperation>()
        {
            new CapturedTransportOperation(outgoingMessage, "A")
        };
        return messages;
    }

    [Test]
    [Explicit]
    public async Task Cannot_close_empty_epoch()
    {
        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
        {
            await conn.OpenAsync().ConfigureAwait(false);
            var (lo, hi) = await persistence.TryClose("A", 0, 6, operation =>
            {
                Console.WriteLine(operation.MessageId);
                return Task.CompletedTask;
            }, conn);

            Assert.AreEqual(0, lo);
            Assert.AreEqual(6, hi);
        }
    }

    [Test]
    public async Task Can_close_non_empty_table()
    {
        var sequences = new Dictionary<string, long>();

        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persistence.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M2"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M4"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

            var (lo, hi) = await persistence.TryClose("A", 0, 6, operation =>
            {
                Console.WriteLine(operation.MessageId);
                return Task.CompletedTask;
            }, conn);

            await persistence.Store(CreateMessages("M5"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

            Assert.AreEqual(3, lo);
            Assert.AreEqual(9, hi);
        }
    }

    [Test]
    public async Task It_refuses_to_insert_when_full()
    {
        var sequences = new Dictionary<string, long>();

        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persistence.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M2"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M4"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M5"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
            await persistence.Store(CreateMessages("M6"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

            await persistence.Store(CreateMessages("M7"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);
        }
    }

    [Test]
    public async Task It_fills_gaps_before_closing()
    {
        var sequences = new Dictionary<string, long>();

        using (var conn = new SqlConnection("data source = (local); initial catalog=test1; integrated security=true"))
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persistence.Store(CreateMessages("M1"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

            await GetNextSequenceValue("A", conn, null);

            await persistence.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

            var (lo, hi) = await persistence.TryClose("A", 0, 6, operation =>
            {
                Console.WriteLine(operation.MessageId);
                return Task.CompletedTask;
            }, conn);

            await persistence.Store(CreateMessages("M3"), (key, value) => { sequences[key] = value; }, conn, null).ConfigureAwait(false);

            Assert.AreEqual(3, lo);
            Assert.AreEqual(9, hi);
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
