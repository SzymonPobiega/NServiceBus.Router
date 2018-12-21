namespace NServiceBus.Router.Deduplication.Inbox
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Logging;

    class InboxTable
    {
        public static string Left(string sourceKey, string destinationKey) => $"Inbox_{sourceKey}_{destinationKey}_Left";
        public static string Right(string sourceKey, string destinationKey) => $"Inbox_{sourceKey}_{destinationKey}_Right";

        static ILog log = LogManager.GetLogger<InboxTable>();
        string name;

        public InboxTable(string name)
        {
            this.name = name;
        }

        public async Task Insert(string messageId, long seq, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($@"
insert into [{name}] (Seq, MessageId) values (@seq, @messageId)", conn, trans))
            {
                command.Parameters.AddWithValue("@seq", seq);
                command.Parameters.AddWithValue("@messageId", messageId);

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<bool> HasHoles(SessionState sessionState, SqlConnection conn, SqlTransaction trans)
        {
            var epochSize = sessionState.Hi - sessionState.Lo;

            //We can use a count query here because there is a check that ensure that inserting a message that is outside
            //of watermarks cannot be committed. If we count the non-locked (comitted) messages and the count is equal to epoch
            //it means there is no holes
            using (var command = new SqlCommand($"select count(Seq) from [{name}] with(readpast)", conn, trans))
            {
                var count = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);

                var hasHoles = count < epochSize;
                return hasHoles;
            }
        }

//        async Task<bool> HasHoles(string tableName, long highWaterMark, SqlConnection conn)
//        {
//            using (var command = new SqlCommand($@"
//select PrevSeq, Seq, NextSeq
//from (select Seq, LAG(Seq) over (order by Seq) PrevSeq, LEAD(Seq) over (order by Seq) NextSeq from [{tableName}] with(nolock)) q 
//	where (PrevSeq is null and Seq >= @lo) 
//    or (NextSeq is null and Seq < @hi) 
//    or (PrevSeq <> Seq - 1) 
//    or (NextSeq <> Seq + 1)
//order by Seq
//", conn))
//            {
//                var hi = highWaterMark - epochSize - 1;
//                var lo = highWaterMark - epochSize - epochSize;
//                command.Parameters.AddWithValue("@hi", hi);
//                command.Parameters.AddWithValue("@lo", lo);
//                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
//                {
//                    while (reader.Read())
//                    {
//                        if (reader.IsDBNull(0))
//                        {
//                            return reader.GetInt64(1) > lo;
//                        }
//                        if (reader.IsDBNull(2))
//                        {
//                            return reader.GetInt64(1) < hi;
//                        }
//                        else
//                        {
//                            return reader.GetInt64(1) + 1 != reader.GetInt64(2);
//                        }
//                    }
//                }
//            }
//            return false;
//        }

        public async Task Truncate(SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"truncate table [{name}]", conn, trans))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task CreateConstraint(long lo, long hi, SqlConnection conn, SqlTransaction trans)
        {
            log.Debug($"Creating constraint in table {name} for range [{lo}, {hi})");

            var constraintName = $"{name}_{lo}_{hi}";

            var commandText = $@"
if not exists (select * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS where constraint_catalog = DB_NAME() and CONSTRAINT_NAME = '{constraintName}' and TABLE_NAME = '{name}')
begin
   alter table [{name}] with nocheck add constraint [{constraintName}] check (([Seq]>=({lo}) AND [Seq]<({hi})));
end
";
            using (var command = new SqlCommand(commandText, conn, trans))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task DropConstraint(long lo, long hi, SqlConnection conn, SqlTransaction trans)
        {
            log.Debug($"Dropping constraint in table {name} for range [{lo}, {hi})");
            var constraintName = $"{name}_{lo}_{hi}";

            var commandText = $@"
if exists (select * FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS where constraint_catalog = DB_NAME() and CONSTRAINT_NAME = '{constraintName}' and TABLE_NAME = '{name}')
begin
   alter table [{name}] drop constraint [{constraintName}];
end
";
            using (var command = new SqlCommand(commandText, conn, trans))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public Task Drop(SqlConnection conn, SqlTransaction trans)
        {
            var script = $@"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[{name}]')
        AND type = 'U')
DROP TABLE [dbo].[{name}]
";
            using (var command = new SqlCommand(script, conn, trans))
            {
                return command.ExecuteNonQueryAsync();
            }
        }

        public Task Create(SqlConnection conn, SqlTransaction trans)
        {
            var script = $@"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[{name}]')
        AND type = 'U')
RETURN

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
    }
}