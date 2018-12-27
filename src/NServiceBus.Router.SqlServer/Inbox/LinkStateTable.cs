namespace NServiceBus.Router.Deduplication.Inbox
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class LinkStateTable
    {
        string destinationKey;

        public LinkStateTable(string destinationKey)
        {
            this.destinationKey = destinationKey;
        }

        public Task InitializeLink(string sourceKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($@"
if exists (select * from [Inbox_LinkState_{destinationKey}] where Source = @source)
    return

insert into [Inbox_LinkState_{destinationKey}] 
(Source, Epoch, HeadLo, HeadHi, HeadTable, TailLo, TailHi, TailTable) 
values 
(@source, 0, 0, 0, NULL, 0, 0, NULL)", conn, trans))
            {
                command.Parameters.AddWithValue("@source", sourceKey);
                return command.ExecuteNonQueryAsync();
            }
        }

        public async Task<LinkState> Get(string sourceKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"select Epoch, HeadLo, HeadHi, HeadTable, TailLo, TailHi, TailTable from [Inbox_LinkState_{destinationKey}] where Source = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", sourceKey);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (!reader.Read())
                    {
                        throw new Exception($"No link state for {sourceKey}");
                    }

                    return LinkState.Hydrate(reader);
                }
            }
        }

        public async Task<LinkState> Lock(string sourceKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"select Epoch, HeadLo, HeadHi, HeadTable, TailLo, TailHi, TailTable from [Inbox_LinkState_{destinationKey}] with (updlock) where Source = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", sourceKey);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (!reader.Read())
                    {
                        throw new Exception($"No link state for {sourceKey}");
                    }

                    return LinkState.Hydrate(reader);
                }
            }
        }

        public async Task Update(string sourceKey, LinkState newLinkState, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($@"
update [Inbox_LinkState_{destinationKey}] set 
Epoch = @epoch, 
HeadLo = @headLo,
HeadHi = @headHi,
HeadTable = @headTable,
TailLo = @tailLo,
TailHi = @tailHi,
TailTable = @tailTable
where Source = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", sourceKey);
                command.Parameters.AddWithValue("@epoch", newLinkState.Epoch);
                command.Parameters.AddWithValue("@headLo", newLinkState.HeadSession.Lo);
                command.Parameters.AddWithValue("@headHi", newLinkState.HeadSession.Hi);
                command.Parameters.AddWithValue("@headTable", newLinkState.HeadSession.Table);
                command.Parameters.AddWithValue("@tailLo", newLinkState.TailSession.Lo);
                command.Parameters.AddWithValue("@tailHi", newLinkState.TailSession.Hi);
                command.Parameters.AddWithValue("@tailTable", newLinkState.TailSession.Table);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public Task Drop(SqlConnection conn, SqlTransaction trans)
        {
            var script = $@"
IF EXISTS (
    SELECT *
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[Inbox_LinkState_{destinationKey}]')
        AND type = 'U')
DROP TABLE [dbo].[Inbox_LinkState_{destinationKey}]
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
    WHERE object_id = OBJECT_ID(N'[dbo].[Inbox_LinkState_{destinationKey}]')
        AND type = 'U')
RETURN

CREATE TABLE [dbo].[Inbox_LinkState_{destinationKey}](
    [Source] [nvarchar](200) NOT NULL,
    [Epoch] [bigint] NOT NULL,
    [HeadLo] [bigint] NOT NULL,
    [HeadHi] [bigint] NOT NULL,
    [HeadTable] [varchar](500) NULL,
    [TailLo] [bigint] NOT NULL,
    [TailHi] [bigint] NOT NULL,
    [TailTable] [varchar](500) NULL,
CONSTRAINT [PK_Inbox_LinkState_{destinationKey}] PRIMARY KEY CLUSTERED 
(
	[Source] ASC
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