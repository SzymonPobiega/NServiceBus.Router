using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.DelayedDelivery;
using NServiceBus.DeliveryConstraints;
using NServiceBus.Logging;
using NServiceBus.Performance.TimeToBeReceived;
using NServiceBus.Router.Deduplication;
using NServiceBus.Routing;
using NServiceBus.Transport;
using TransportOperation = NServiceBus.Transport.TransportOperation;

class OutboxPersistence
{
    int epochSize;
    static ILog log = LogManager.GetLogger<OutboxPersistence>();

    public OutboxPersistence(int epochSize)
    {
        this.epochSize = epochSize;
    }

    public async Task Install(string sequenceKey, SqlConnection conn, SqlTransaction trans)
    {
        var lo = $"Outbox_{sequenceKey}_1";
        var hi = $"Outbox_{sequenceKey}_2";

        await CreateWatermarksTable(conn, trans).ConfigureAwait(false);

        await CreateTable(lo, conn, trans).ConfigureAwait(false);
        await CreateConstraint(lo, 0, epochSize, conn, trans).ConfigureAwait(false);
        await CreateTable(hi, conn, trans).ConfigureAwait(false);
        await CreateConstraint(hi, epochSize, 2 * epochSize, conn, trans).ConfigureAwait(false);

        await CreateSequence($"Sequence_{sequenceKey}", conn, trans).ConfigureAwait(false);

        await InsertWatermarks(sequenceKey, 0, 2 * epochSize, conn, trans);
    }

    static Task InsertWatermarks(string sequence, long lo, long hi, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand(@"insert into WaterMarks (Destination, Lo, Hi) values (@dest, @lo, @hi)", conn, trans))
        {
            command.Parameters.AddWithValue("@dest", sequence);
            command.Parameters.AddWithValue("lo", lo);
            command.Parameters.AddWithValue("hi", hi);
            return command.ExecuteNonQueryAsync();
        }
    }

    static Task CreateWatermarksTable(SqlConnection conn, SqlTransaction trans)
    {
        var script = @"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[WaterMarks]')
        AND type = 'U')
DROP TABLE [dbo].[WaterMarks]

CREATE TABLE [dbo].[WaterMarks](
	[Destination] [nvarchar](200) NOT NULL,
	[Lo] [bigint] NOT NULL,
	[Hi] [bigint] NOT NULL
) ON [PRIMARY]
";
        using (var command = new SqlCommand(script, conn, trans))
        {
            return command.ExecuteNonQueryAsync();
        }
    }

    static Task CreateTable(string name, SqlConnection conn, SqlTransaction trans)
    {
        var script = $@"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[{name}]')
        AND type = 'U')
DROP TABLE [dbo].[{name}]

CREATE TABLE [dbo].[{name}] (
	[Seq] [bigint] NOT NULL,
	[MessageId] [varchar](200) NOT NULL,
	[Headers] [nvarchar](max) NULL,
	[Body] [varbinary](max) NULL,
	[Options] [varchar](max) NULL,
	[Dispatched] [bit] NOT NULL,
	[IsPlug] [bit] NOT NULL,
 CONSTRAINT [PK_{name}] PRIMARY KEY CLUSTERED 
(
	[Seq] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
";
        using (var command = new SqlCommand(script, conn, trans))
        {
            return command.ExecuteNonQueryAsync();
        }
    }

    Task CreateSequence(string name, SqlConnection conn, SqlTransaction trans)
    {
        var script = $@"
IF EXISTS (
    SELECT * 
    FROM sys.objects 
    WHERE object_id = OBJECT_ID(N'[dbo].[{name}]') 
        AND type = 'SO')
DROP SEQUENCE [dbo].[{name}]

CREATE SEQUENCE [dbo].[{name}] AS [bigint] START WITH 0 INCREMENT BY 1";
        using (var command = new SqlCommand(script, conn, trans))
        {
            return command.ExecuteNonQueryAsync();
        }
    }

    public async Task Store(List<CapturedTransportOperation> capturedMessages, Action<string, long> updateSequence, SqlConnection connection, SqlTransaction transaction)
    {
        foreach (var op in capturedMessages)
        {
            await Store(op, updateSequence, connection, transaction);
        }
    }

    async Task Store(CapturedTransportOperation operation, Action<string, long> updateSequence, SqlConnection conn, SqlTransaction trans)
    {
        var sequenceKey = operation.Destination;
        var message = Convert(operation.Operation, operation.Destination);

        var seq = await GetNextSequenceValue(sequenceKey, conn, trans).ConfigureAwait(false);

        operation.AssignSequence(seq);
        updateSequence(sequenceKey, seq);

        var tableName = GetTableName(seq, sequenceKey);

        await Insert(message, tableName, seq, conn, trans);
    }

    string GetTableName(long seq, string sequenceKey)
    {
        var side = (seq / epochSize) % 2;

        var tableName = $"Outbox_{sequenceKey}_{side + 1}";
        return tableName;
    }

    static async Task Insert(PersistentOutboxTransportOperation message, string tableName, long seq, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($@"
insert into [{tableName}] (Seq, MessageId, Headers, Body, Options, Dispatched, IsPlug) values (@seq, @messageId, @headers, @body, @options, 0, 0);",
            conn, trans))
        {
            command.Parameters.AddWithValue("@seq", seq);
            command.Parameters.AddWithValue("@messageId", message.MessageId);
            command.Parameters.AddWithValue("@headers", DictionarySerializer.Serialize(message.Headers));
            command.Parameters.AddWithValue("@body", message.Body);
            command.Parameters.AddWithValue("@options", DictionarySerializer.Serialize(message.Options));
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public async Task<(long Lo, long Hi)> TryClose(string sequenceKey, long prevLo, long prevHi,
        Func<TransportOperation, Task> dispatch, SqlConnection conn)
    {
        //Let's actually check if our values are correct.
        var (lo, hi) = await GetWaterMarks(sequenceKey, conn);

        if (lo != prevLo || hi != prevHi) //The values changed. Please re-evaluate if we need to close.
        {
            log.Debug($"The watermarks for sequence {sequenceKey} were outdated. New values are lo={lo},hi={hi}.");
            return (lo, hi);
        }

        var tableName = GetTableName(lo, sequenceKey);

        if (await HasHoles(tableName, conn).ConfigureAwait(false))
        {
            log.Debug($"Epoch table {tableName} seems to have holes in the sequence. Attempting to plug them.");
            var holes = await FindHoles(tableName, hi, conn).ConfigureAwait(false);

            if (!holes.Any())
            {
                log.Debug($"Cannot close epoch table {tableName}. Table is empty.");
                return (lo, hi);
            }

            //Plug missing row holes by inserting dummy rows
            foreach (var hole in holes.Where(h => h.Type == HoleType.MissingRow))
            {
                //If we blow here it means that some other process inserted rows after we looked for holes. We backtrack and come back
                log.Debug($"Plugging hole {hole.Id} with a dummy message row.");
                await PlugHole(tableName, hole.Id, conn).ConfigureAwait(false);
            }

            //Dispatch all the holes and mark them as dispatched
            foreach (var hole in holes)
            {
                TransportOperation op;
                if (hole.Type == HoleType.MissingRow)
                {
                    op = CreatePlugOperation(hole.Id, sequenceKey);
                    log.Debug($"Dispatching dummy message row {hole.Id}.");
                }
                else
                {
                    op = await LoadOperationById(hole.Id, tableName, conn).ConfigureAwait(false);
                    log.Debug($"Dispatching message {hole.Id} with ID {op.Message.MessageId}.");
                }

                await dispatch(op).ConfigureAwait(false);
                await MarkAsDispatched(tableName, hole.Id, conn, null).ConfigureAwait(false);
            }
        }

        log.Debug($"Closing epoch table {tableName}.");
        using (var closeTransaction = conn.BeginTransaction())
        {
            //Ensure only one process can enter here
            var (lockedLo, lockedHi) = await GetWaterMarksWithLock(sequenceKey, conn, closeTransaction);
            if (lo != lockedLo || hi != lockedHi)
            {
                log.Debug($"Watermark values read in transaction don't match previous values. Somebody else has closed epoch table {tableName}.");
                return (lockedLo, lockedHi);
            }

            var newLo = lockedLo + epochSize;
            var newHi = lockedHi + epochSize;

            await CreateConstraint(tableName, newLo + epochSize, newHi, conn, closeTransaction);

            //Here we have all holes plugged and no possibility of inserting new rows. We can truncate
            log.Debug($"Truncating table {tableName}.");
            await Truncate(tableName, conn, closeTransaction).ConfigureAwait(false);
            await DropConstraint(tableName, lockedLo, newLo, conn, closeTransaction).ConfigureAwait(false);

            log.Debug($"Updating watermark values for table {tableName} to {newLo},{newHi}.");
            await UpdateWaterMarks(sequenceKey, newLo, newHi, conn, closeTransaction).ConfigureAwait(false);

            closeTransaction.Commit();
            return (newLo, newHi);
        }
    }

    static TransportOperation CreatePlugOperation(long seq, string destination)
    {
        var headers = new Dictionary<string, string>
        {
            ["NServiceBus.Router.Sequence"] = seq.ToString(),
            ["NServiceBus.Router.Plug"] = "true"
        };
        var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, new byte[0]);
        return new TransportOperation(message, new UnicastAddressTag(destination));
    }

    static async Task<TransportOperation> LoadOperationById(long seq, string tableName, SqlConnection conn)
    {
        using (var command = new SqlCommand($@"
select MessageId, Headers, Body, Options from [{tableName}] where Seq = @seq
", conn))
        {
            command.Parameters.AddWithValue("@seq", seq);

            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (!reader.Read())
                {
                    throw new Exception($"Message with sequence {seq} not found.");
                }

                var messageId = reader.GetString(0);
                var headersSerialized = await GetText(reader, 1);
                var body = await GetBody(reader, 2);
                var options = await GetText(reader, 3);

                var message = new PersistentOutboxTransportOperation(messageId, DictionarySerializer.Deserialize(options), body, DictionarySerializer.Deserialize(headersSerialized));
                return Convert(message);
            }
        }
    }

    static async Task<string> GetText(SqlDataReader dataReader, int headersIndex)
    {
        if (await dataReader.IsDBNullAsync(headersIndex).ConfigureAwait(false))
        {
            return null;
        }

        using (var textReader = dataReader.GetTextReader(headersIndex))
        {
            return await textReader.ReadToEndAsync().ConfigureAwait(false);
        }
    }

    static async Task<byte[]> GetBody(SqlDataReader dataReader, int bodyIndex)
    {
        // Null values will be returned as an empty (zero bytes) Stream.
        using (var outStream = new MemoryStream())
        using (var stream = dataReader.GetStream(bodyIndex))
        {
            await stream.CopyToAsync(outStream).ConfigureAwait(false);
            return outStream.ToArray();
        }
    }

    static async Task CreateConstraint(string tableName, long lo, long hi, SqlConnection conn, SqlTransaction trans)
    {
        log.Debug($"Creating constraing in table {tableName} for range [{lo}, {hi})");

        var constraintName = $"{tableName}_{lo}_{hi}";

        var commandText = $@"
if not exists (select * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS where constraint_catalog = DB_NAME() and CONSTRAINT_NAME = '{constraintName}' and TABLE_NAME = '{tableName}')
begin
   alter table [{tableName}] with nocheck add constraint [{constraintName}] check (([Seq]>=({lo}) AND [Seq]<({hi})));
end
";
        using (var command = new SqlCommand(commandText, conn, trans))
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    static async Task DropConstraint(string tableName, long lo, long hi, SqlConnection conn, SqlTransaction trans)
    {
        log.Debug($"Dropping constraing in table {tableName} for range [{lo}, {hi})");
        var constraintName = $"{tableName}_{lo}_{hi}";

        var commandText = $@"
if exists (select * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS where constraint_catalog = DB_NAME() and CONSTRAINT_NAME = '{constraintName}' and TABLE_NAME = '{tableName}')
begin
   alter table [{tableName}] drop constraint [{constraintName}];
end
";
        using (var command = new SqlCommand(commandText, conn, trans))
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    static async Task PlugHole(string tableName, long holeId, SqlConnection conn)
    {
        using (var command = new SqlCommand($"insert into [{tableName}] (Seq, MessageId, IsPlug, Dispatched) values (@seq, @id, 1, 0)", conn))
        {
            command.Parameters.AddWithValue("@seq", holeId);
            command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    static async Task MarkAsDispatched(string tableName, long seq, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($"update [{tableName}] set Dispatched = 1 where Seq = @seq", conn, trans))
        {
            command.Parameters.AddWithValue("@seq", seq);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    async Task<bool> HasHoles(string tableName, SqlConnection conn)
    {
        using (var command = new SqlCommand($"select count(Seq) from [{tableName}] with(nolock) where Dispatched = 1", conn))
        {
            var count = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);

            return count != epochSize;
        }
    }

    static async Task Truncate(string tableName, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($"truncate table [{tableName}]", conn, trans))
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    enum HoleType
    {
        MissingRow,
        UndispatchedRow
    }

    async Task<List<(long Id, HoleType Type)>> FindHoles(string tableName, long highWaterMark, SqlConnection conn)
    {
        var holes = new List<(long Id, HoleType Type)>();
        using (var command = new SqlCommand($@"
select PrevSeq, Seq, NextSeq, Dispatched
from (select Seq, LAG(Seq) over (order by Seq) PrevSeq, LEAD(Seq) over (order by Seq) NextSeq, Dispatched from [{tableName}]) q 
	where (PrevSeq is null and Seq > @lo) 
    or (NextSeq is null and Seq < @hi) 
    or (PrevSeq <> Seq - 1) 
    or (NextSeq <> Seq + 1)
    or (Dispatched = 0)
order by Seq
", conn))
        {
            var hi = highWaterMark - epochSize - 1;
            var lo = highWaterMark - epochSize - epochSize;
            command.Parameters.AddWithValue("@hi", hi);
            command.Parameters.AddWithValue("@lo", lo);
            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (reader.Read())
                {
                    var dispatched = reader.GetBoolean(3);
                    if (!dispatched)
                    {
                        holes.Add((reader.GetInt64(1), HoleType.UndispatchedRow));
                    }

                    if (reader.IsDBNull(0))
                    {
                        AddHoles(0, reader.GetInt64(1) - 1, holes);
                    }
                    if (reader.IsDBNull(2))
                    {
                        AddHoles(reader.GetInt64(1) + 1, hi, holes);
                    }
                    else
                    {
                        AddHoles(reader.GetInt64(1) + 1, reader.GetInt64(2) - 1, holes);
                    }
                }
            }
        }
        return holes;
    }

    static void AddHoles(long l, long h, ICollection<(long Id, HoleType Type)> holes)
    {
        for (var i = l; i <= h; i++)
        {
            holes.Add((i, HoleType.MissingRow));
        }
    }

    static async Task<(long Lo, long High)> GetWaterMarks(string sequenceKey, SqlConnection conn)
    {
        using (var command = new SqlCommand("select Lo, Hi from WaterMarks where Destination = @key", conn))
        {
            command.Parameters.AddWithValue("@key", sequenceKey);
            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (!reader.Read())
                {
                    throw new Exception($"No water marks for sequence {sequenceKey}");
                }
                return (reader.GetInt64(0), reader.GetInt64(1));
            }
        }
    }

    static async Task<(long Lo, long High)> GetWaterMarksWithLock(string sequenceKey, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand("select Lo, Hi from WaterMarks with (updlock) where Destination = @key", conn, trans))
        {
            command.Parameters.AddWithValue("@key", sequenceKey);
            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                if (!reader.Read())
                {
                    throw new Exception($"No water marks for sequence {sequenceKey}");
                }
                return (reader.GetInt64(0), reader.GetInt64(1));
            }
        }
    }

    static async Task UpdateWaterMarks(string sequenceKey, long lo, long hi, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand("update WaterMarks set Lo = @lo, Hi = @hi where Destination = @key", conn, trans))
        {
            command.Parameters.AddWithValue("@key", sequenceKey);
            command.Parameters.AddWithValue("@lo", lo);
            command.Parameters.AddWithValue("@hi", hi);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

    public async Task MarkDispatched(CapturedTransportOperation operation, SqlConnection conn, SqlTransaction trans)
    {
        var tableName = GetTableName(operation.Sequence, operation.Destination);
        using (var command = new SqlCommand($"update [{tableName}] set Dispatched = 1 where Seq = @seq", conn, trans))
        {
            command.Parameters.AddWithValue("@seq", operation.Sequence);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    static TransportOperation Convert(PersistentOutboxTransportOperation persistentOp)
    {
        var message = new OutgoingMessage(persistentOp.MessageId, persistentOp.Headers, persistentOp.Body);
        return new TransportOperation(
            message,
            DeserializeRoutingStrategy(persistentOp.Options),
            DispatchConsistency.Isolated,
            DeserializeConstraints(persistentOp.Options));
    }

    static PersistentOutboxTransportOperation Convert(TransportOperation operation, string destination)
    {
        var options = new Dictionary<string, string>();

        foreach (var constraint in operation.DeliveryConstraints)
        {
            SerializeDeliveryConstraint(constraint, options);
        }

        options["Destination"] = destination;

        var persistentOp = new PersistentOutboxTransportOperation(operation.Message.MessageId, options, operation.Message.Body, operation.Message.Headers);
        return persistentOp;
    }

    static void SerializeDeliveryConstraint(DeliveryConstraint constraint, Dictionary<string, string> options)
    {
        if (constraint is NonDurableDelivery)
        {
            options["NonDurable"] = true.ToString();
            return;
        }
        if (constraint is DoNotDeliverBefore doNotDeliverBefore)
        {
            options["DeliverAt"] = DateTimeExtensions.ToWireFormattedString(doNotDeliverBefore.At);
            return;
        }

        if (constraint is DelayDeliveryWith delayDeliveryWith)
        {
            options["DelayDeliveryFor"] = delayDeliveryWith.Delay.ToString();
            return;
        }

        if (constraint is DiscardIfNotReceivedBefore discard)
        {
            options["TimeToBeReceived"] = discard.MaxTime.ToString();
            return;
        }

        throw new Exception($"Unknown delivery constraint {constraint.GetType().FullName}");
    }

    static List<DeliveryConstraint> DeserializeConstraints(Dictionary<string, string> options)
    {
        var constraints = new List<DeliveryConstraint>(4);
        if (options.ContainsKey("NonDurable"))
        {
            constraints.Add(new NonDurableDelivery());
        }

        if (options.TryGetValue("DeliverAt", out var deliverAt))
        {
            constraints.Add(new DoNotDeliverBefore(DateTimeExtensions.ToUtcDateTime(deliverAt)));
        }

        if (options.TryGetValue("DelayDeliveryFor", out var delay))
        {
            constraints.Add(new DelayDeliveryWith(TimeSpan.Parse(delay)));
        }

        if (options.TryGetValue("TimeToBeReceived", out var ttbr))
        {
            constraints.Add(new DiscardIfNotReceivedBefore(TimeSpan.Parse(ttbr)));
        }
        return constraints;
    }

    static AddressTag DeserializeRoutingStrategy(Dictionary<string, string> options)
    {
        if (options.TryGetValue("Destination", out var destination))
        {
            return new UnicastAddressTag(destination);
        }

        if (options.TryGetValue("EventType", out var eventType))
        {
            return new MulticastAddressTag(Type.GetType(eventType, true));
        }

        throw new Exception("Could not find routing strategy to deserialize");
    }
}
