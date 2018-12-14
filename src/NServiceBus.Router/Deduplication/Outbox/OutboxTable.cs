namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading.Tasks;
    using Logging;
    using Transport;

    class OutboxTable
    {
        public static string Left(string sourceKey, string destinationKey) => $"Outbox_{sourceKey}_{destinationKey}_Left";
        public static string Right(string sourceKey, string destinationKey) => $"Outbox_{sourceKey}_{destinationKey}_Right";

        static ILog log = LogManager.GetLogger<OutboxTable>();
        public string Name { get; }

        public OutboxTable(string name)
        {
            this.Name = name;
        }

        public Task Create(SqlConnection conn, SqlTransaction trans)
        {
            var script = $@"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[{Name}]')
        AND type = 'U')
RETURN

CREATE TABLE [dbo].[{Name}] (
	[Seq] [bigint] NOT NULL,
	[MessageId] [varchar](200) NOT NULL,
	[Headers] [nvarchar](max) NULL,
	[Body] [varbinary](max) NULL,
	[Dispatched] [bit] NOT NULL,
	[IsPlug] [bit] NOT NULL,
 CONSTRAINT [PK_{Name}] PRIMARY KEY CLUSTERED 
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

        public Task Drop(SqlConnection conn, SqlTransaction trans)
        {
            var script = $@"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[{Name}]')
        AND type = 'U')
DROP TABLE [dbo].[{Name}]
";
            using (var command = new SqlCommand(script, conn, trans))
            {
                return command.ExecuteNonQueryAsync();
            }
        }

        public async Task CreateConstraint(long lo, long hi, SqlConnection conn, SqlTransaction trans)
        {
            log.Debug($"Creating constraint in table {Name} for range [{lo}, {hi})");

            var constraintName = $"{Name}_{lo}_{hi}";

            var commandText = $@"
if not exists (select * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS where constraint_catalog = DB_NAME() and CONSTRAINT_NAME = '{constraintName}' and TABLE_NAME = '{Name}')
begin
   alter table [{Name}] with nocheck add constraint [{constraintName}] check (([Seq]>=({lo}) AND [Seq]<({hi})));
end
";
            using (var command = new SqlCommand(commandText, conn, trans))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task DropConstraint(long lo, long hi, SqlConnection conn, SqlTransaction trans)
        {
            log.Debug($"Dropping constraint in table {Name} for range [{lo}, {hi})");
            var constraintName = $"{Name}_{lo}_{hi}";

            var commandText = $@"
if exists (select * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS where constraint_catalog = DB_NAME() and CONSTRAINT_NAME = '{constraintName}' and TABLE_NAME = '{Name}')
begin
   alter table [{Name}] drop constraint [{constraintName}];
end
";
            using (var command = new SqlCommand(commandText, conn, trans))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> HasHoles(SessionState sessionState, SqlConnection conn)
        {
            var epochSize = sessionState.Hi - sessionState.Lo;

            //We can use a count query here because there is a check that ensure that inserting a message that is outside
            //of current epoch cannot be comitted. If we count the non-locked (comitted) messages and the count is equal to epoch
            //it means there is no holes
            using (var command = new SqlCommand($"select count(Seq) from [{Name}] with(readpast) where Dispatched = 1", conn))
            {
                var count = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);

                var hasHoles = count < epochSize;
                return hasHoles;
            }
        }

        public async Task<List<(long Id, HoleType Type)>> FindHoles(SessionState sessionState, SqlConnection conn)
        {
            var lo = sessionState.Lo;
            var hi = sessionState.Hi;
            var holes = new List<(long Id, HoleType Type)>();
            using (var command = new SqlCommand($@"
select PrevSeq, Seq, NextSeq, Dispatched
from (select Seq, LAG(Seq) over (order by Seq) PrevSeq, LEAD(Seq) over (order by Seq) NextSeq, Dispatched from [{Name}]) q 
	where (PrevSeq is null and Seq >= @lo) 
    or (NextSeq is null and Seq < @hi) 
    or (PrevSeq <> Seq - 1) 
    or (NextSeq <> Seq + 1)
    or (Dispatched = 0)
order by Seq
", conn))
            {
                command.Parameters.AddWithValue("@hi", hi - 1);
                command.Parameters.AddWithValue("@lo", lo);

                var empty = true;
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (reader.Read())
                    {
                        empty = false;
                        var dispatched = reader.GetBoolean(3);
                        if (!dispatched)
                        {
                            holes.Add((reader.GetInt64(1), HoleType.UndispatchedRow));
                        }

                        if (reader.IsDBNull(0))
                        {
                            AddHoles(lo, reader.GetInt64(1), holes);
                        }
                        if (reader.IsDBNull(2))
                        {
                            AddHoles(reader.GetInt64(1) + 1, hi, holes);
                        }
                        else
                        {
                            AddHoles(reader.GetInt64(1) + 1, reader.GetInt64(2), holes);
                        }
                    }
                }
                //If the table is empty, all rows are holes
                if (empty)
                {
                    AddHoles(lo, hi, holes);
                }
            }
            return holes;
        }

        static void AddHoles(long l, long h, ICollection<(long Id, HoleType Type)> holes)
        {
            for (var i = l; i < h; i++)
            {
                holes.Add((i, HoleType.MissingRow));
            }
        }

        public async Task PlugHole(long holeId, SqlConnection conn)
        {
            using (var command = new SqlCommand($"insert into [{Name}] (Seq, MessageId, IsPlug, Dispatched) values (@seq, @id, 1, 0)", conn))
            {
                command.Parameters.AddWithValue("@seq", holeId);
                command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task Insert(PersistentOutboxTransportOperation message, long seq, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($@"
insert into [{Name}] (Seq, MessageId, Headers, Body, Dispatched, IsPlug) values (@seq, @messageId, @headers, @body, 0, 0);",
                conn, trans))
            {
                command.Parameters.AddWithValue("@seq", seq);
                command.Parameters.AddWithValue("@messageId", message.MessageId);
                command.Parameters.AddWithValue("@headers", DictionarySerializer.Serialize(message.Headers));
                command.Parameters.AddWithValue("@body", message.Body);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<OutgoingMessage> LoadMessageById(long seq, SqlConnection conn)
        {
            using (var command = new SqlCommand($@"
select MessageId, Headers, Body from [{Name}] where Seq = @seq
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

                    var headers = DictionarySerializer.Deserialize(headersSerialized);

                    var message = new OutgoingMessage(messageId, headers, body);
                    return message;
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
            if (await dataReader.IsDBNullAsync(bodyIndex).ConfigureAwait(false))
            {
                return null;
            }
            // Null values will be returned as an empty (zero bytes) Stream.
            using (var outStream = new MemoryStream())
            using (var stream = dataReader.GetStream(bodyIndex))
            {
                await stream.CopyToAsync(outStream).ConfigureAwait(false);
                return outStream.ToArray();
            }
        }

        public async Task Truncate(SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"truncate table [{Name}]", conn, trans))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task MarkAsDispatched(long seq, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"update [{Name}] set Dispatched = 1 where Seq = @seq", conn, trans))
            {
                command.Parameters.AddWithValue("@seq", seq);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }
}