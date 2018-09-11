using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.Logging;

class WatermarkViolationException : Exception
{
    public WatermarkViolationException(string message) : base(message)
    {
    }
}

class InboxPersitence
{
    int epochSize;
    static ILog log = LogManager.GetLogger<InboxPersitence>();
    string destinationSequenceKey;

    public InboxPersitence(int epochSize, string destinationSequenceKey)
    {
        this.epochSize = epochSize;
        this.destinationSequenceKey = destinationSequenceKey;
    }

    public async Task Install(string sourceSequenceKey, SqlConnection conn, SqlTransaction trans)
    {
        var lo = $"Inbox_{sourceSequenceKey}_{destinationSequenceKey}_1";
        var hi = $"Inbox_{sourceSequenceKey}_{destinationSequenceKey}_2";

        await CreateWatermarksTable(conn, trans).ConfigureAwait(false);

        await CreateTable(lo, conn, trans).ConfigureAwait(false);
        await CreateConstraint(lo, 0, epochSize, conn, trans).ConfigureAwait(false);
        await CreateTable(hi, conn, trans).ConfigureAwait(false);
        await CreateConstraint(hi, epochSize, 2 * epochSize, conn, trans).ConfigureAwait(false);

        await InsertWatermarks(sourceSequenceKey, 0, 2 * epochSize, conn, trans);
    }

    Task InsertWatermarks(string sequence, long lo, long hi, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($"insert into [Inbox_WaterMarks_{destinationSequenceKey}] (Source, Lo, Hi) values (@dest, @lo, @hi)", conn, trans))
        {
            command.Parameters.AddWithValue("@dest", sequence);
            command.Parameters.AddWithValue("lo", lo);
            command.Parameters.AddWithValue("hi", hi);
            return command.ExecuteNonQueryAsync();
        }
    }

    Task CreateWatermarksTable(SqlConnection conn, SqlTransaction trans)
    {
        var script = $@"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[Inbox_WaterMarks_{destinationSequenceKey}]')
        AND type = 'U')
DROP TABLE [dbo].[Inbox_WaterMarks_{destinationSequenceKey}]

CREATE TABLE [dbo].[Inbox_WaterMarks_{destinationSequenceKey}](
	[Source] [nvarchar](200) NOT NULL,
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
	[MessageId] [varchar](200) NOT NULL
 CONSTRAINT [PK_{name}] PRIMARY KEY CLUSTERED 
(
	[Seq] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
";
        using (var command = new SqlCommand(script, conn, trans))
        {
            return command.ExecuteNonQueryAsync();
        }
    }

    public async Task<InboxDeduplicationResult> Deduplicate(string messageId, int seq, string sourceSequenceKey, SqlConnection conn, SqlTransaction trans)
    {
        var tableName = GetTableName(seq, sourceSequenceKey);
        try
        {
            await Insert(messageId, sourceSequenceKey, seq, tableName, conn, trans);
            return InboxDeduplicationResult.OK;
        }
        catch (SqlException e)
        {
            if (e.Number == 547)
            {
                return InboxDeduplicationResult.WatermarkViolation;
            }

            if (e.Number == 2627)
            {
                return InboxDeduplicationResult.Duplicate;
            }

            throw;
        }
        catch (WatermarkViolationException)
        {
            return InboxDeduplicationResult.WatermarkViolation;
        }
    }

    async Task Insert(string messageId, string sequenceKey, int seq, string tableName, SqlConnection conn, SqlTransaction trans)
    {
//        using (var command = new SqlCommand($@"
//insert into [{tableName}] (Seq, MessageId) values (@seq, @messageId);
//select Lo, Hi from [Inbox_WaterMarks_{destinationSequenceKey}] where Source = @key;", conn, trans))
//        {
//            command.Parameters.AddWithValue("@seq", seq);
//            command.Parameters.AddWithValue("@key", sequenceKey);
//            command.Parameters.AddWithValue("@messageId", messageId);
//            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
//            {
//                if (!reader.Read())
//                {
//                    throw new Exception($"No water marks for sequence {sequenceKey}");
//                }

//                var lo = reader.GetInt64(0);
//                var hi = reader.GetInt64(1);
//                if (seq < lo || seq >= hi)
//                {
//                    throw new WatermarkViolationException($"Sequence {seq} value outside of watermarks [{lo}, {hi})");
//                }
//            }
//        }

        using (var command = new SqlCommand($@"
insert into [{tableName}] (Seq, MessageId) values (@seq, @messageId)", conn, trans))
        {
            command.Parameters.AddWithValue("@seq", seq);
            command.Parameters.AddWithValue("@key", sequenceKey);
            command.Parameters.AddWithValue("@messageId", messageId);

            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    public async Task<(long Lo, long Hi)> TryClose(string sourceSequenceKey, long prevLo, long prevHi, SqlConnection conn)
    {
        //Let's actually check if our values are correct.
        var (lo, hi) = await GetWaterMarks(sourceSequenceKey, conn);

        if (lo != prevLo || hi != prevHi) //The values changed. Please re-evaluate if we need to close.
        {
            log.Debug($"The inbox watermarks for sequence {sourceSequenceKey} were outdated. New values are lo={lo},hi={hi}.");
            return (lo, hi);
        }

        var tableName = GetTableName(lo, sourceSequenceKey);

        if (await HasHoles(tableName, conn).ConfigureAwait(false))
        {
            log.Debug($"Inbox table {tableName} seems to have holes in the sequence. Cannot close yet.");
            return (lo, hi);
        }

        log.Debug($"Closing inbox table {tableName}.");
        using (var closeTransaction = conn.BeginTransaction())
        {
            //Ensure only one process can enter here
            var (lockedLo, lockedHi) = await GetWaterMarksWithLock(sourceSequenceKey, conn, closeTransaction);
            if (lo != lockedLo || hi != lockedHi)
            {
                log.Debug($"Watermark values read in transaction don't match previous values. Somebody else has closed inbox table {tableName}.");
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
            await UpdateWaterMarks(sourceSequenceKey, newLo, newHi, conn, closeTransaction).ConfigureAwait(false);

            closeTransaction.Commit();
            return (newLo, newHi);
        }
    }

    string GetTableName(long seq, string sourceSequenceKey)
    {
        var side = (seq / epochSize) % 2;

        var tableName = $"Inbox_{sourceSequenceKey}_{destinationSequenceKey}_{side + 1}";
        return tableName;
    }

    async Task<bool> HasHoles(string tableName, SqlConnection conn)
    {
        //We can use a count query here because there is a check that ensure that inserting a message that is outside
        //of watermarks cannot be comitted. If we count the non-locked (comitted) messages and the count is equal to epoch
        //it means there is no holes
        using (var command = new SqlCommand($"select count(Seq) from [{tableName}] with(readpast)", conn))
        {
            var count = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);

            var hasHoles = count < epochSize;
            return hasHoles;
        }
    }

    async Task<bool> HasHoles(string tableName, long highWaterMark, SqlConnection conn)
    {
        using (var command = new SqlCommand($@"
select PrevSeq, Seq, NextSeq
from (select Seq, LAG(Seq) over (order by Seq) PrevSeq, LEAD(Seq) over (order by Seq) NextSeq from [{tableName}] with(nolock)) q 
	where (PrevSeq is null and Seq >= @lo) 
    or (NextSeq is null and Seq < @hi) 
    or (PrevSeq <> Seq - 1) 
    or (NextSeq <> Seq + 1)
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
                    if (reader.IsDBNull(0))
                    {
                        return reader.GetInt64(1) > lo;
                    }
                    if (reader.IsDBNull(2))
                    {
                        return reader.GetInt64(1) < hi;
                    }
                    else
                    {
                        return reader.GetInt64(1) + 1 != reader.GetInt64(2);
                    }
                }
            }
        }
        return false;
    }

    async Task<(long Lo, long High)> GetWaterMarks(string sequenceKey, SqlConnection conn)
    {
        using (var command = new SqlCommand($"select Lo, Hi from [Inbox_WaterMarks_{destinationSequenceKey}] where Source = @key", conn))
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

    async Task<(long Lo, long High)> GetWaterMarksWithLock(string sequenceKey, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($"select Lo, Hi from [Inbox_WaterMarks_{destinationSequenceKey}] with (xlock) where Source = @key", conn, trans))
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

    async Task UpdateWaterMarks(string sequenceKey, long lo, long hi, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($"update [Inbox_WaterMarks_{destinationSequenceKey}] set Lo = @lo, Hi = @hi where Source = @key", conn, trans))
        {
            command.Parameters.AddWithValue("@key", sequenceKey);
            command.Parameters.AddWithValue("@lo", lo);
            command.Parameters.AddWithValue("@hi", hi);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    static async Task Truncate(string tableName, SqlConnection conn, SqlTransaction trans)
    {
        using (var command = new SqlCommand($"truncate table [{tableName}]", conn, trans))
        {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
}
