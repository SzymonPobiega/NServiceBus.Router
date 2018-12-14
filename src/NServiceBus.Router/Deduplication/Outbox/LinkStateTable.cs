namespace NServiceBus.Router.Deduplication.Outbox
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class LinkStateTable
    {
        string sourceKey;

        public LinkStateTable(string sourceKey)
        {
            this.sourceKey = sourceKey;
        }

        public Task InitializeLink(string destinationKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($@"
if exists (select * from [Outbox_LinkState_{sourceKey}] where Destination = @dest)
    return

insert into [Outbox_LinkState_{sourceKey}] 
(Destination, Announced, Epoch, HeadLo, HeadHi, HeadTable, TailLo, TailHi, TailTable) 
values 
(@dest, 0, 0, 0, 0, NULL, 0, 0, NULL)", conn, trans))
            {
                command.Parameters.AddWithValue("@dest", destinationKey);
                return command.ExecuteNonQueryAsync();
            }
        }

        public async Task<LinkState> Get(string destinationKey, SqlConnection conn, SqlTransaction trans = null)
        {
            using (var command = new SqlCommand($"select Epoch, Announced, HeadLo, HeadHi, HeadTable, TailLo, TailHi, TailTable from [Outbox_LinkState_{sourceKey}] where Destination = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", destinationKey);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (!reader.Read())
                    {
                        throw new Exception($"No link state for {destinationKey}");
                    }

                    return LinkState.Hydrate(reader);
                }
            }
        }

        public async Task<LinkState> Lock(string destinationKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"select Epoch, Announced, HeadLo, HeadHi, HeadTable, TailLo, TailHi, TailTable from [Outbox_LinkState_{sourceKey}] with (updlock) where Destination = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", destinationKey);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (!reader.Read())
                    {
                        throw new Exception($"No link state for {destinationKey}");
                    }

                    return LinkState.Hydrate(reader);
                }
            }
        }

        public async Task Update(string destinationKey, LinkState newLinkState, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($@"
update [Outbox_LinkState_{sourceKey}] set 
Epoch = @epoch, 
Announced = @announced,
HeadLo = @headLo,
HeadHi = @headHi,
HeadTable = @headTable,
TailLo = @tailLo,
TailHi = @tailHi,
TailTable = @tailTable
where Destination = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", destinationKey);
                command.Parameters.AddWithValue("@epoch", newLinkState.Epoch);
                command.Parameters.AddWithValue("@announced", newLinkState.IsEpochAnnounced);
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
    WHERE object_id = OBJECT_ID(N'[dbo].[Outbox_LinkState_{sourceKey}]')
        AND type = 'U')
DROP TABLE [dbo].[Outbox_LinkState_{sourceKey}]
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
    WHERE object_id = OBJECT_ID(N'[dbo].[Outbox_LinkState_{sourceKey}]')
        AND type = 'U')
RETURN

CREATE TABLE [dbo].[Outbox_LinkState_{sourceKey}](
    [Destination] [nvarchar](200) NOT NULL,
    [Epoch] [bigint] NOT NULL,
    [Announced] [bit] NOT NULL,
    [HeadLo] [bigint] NOT NULL,
    [HeadHi] [bigint] NOT NULL,
    [HeadTable] [varchar](500) NULL,
    [TailLo] [bigint] NOT NULL,
    [TailHi] [bigint] NOT NULL,
    [TailTable] [varchar](500) NULL,
CONSTRAINT [PK_Outbox_LinkState_{sourceKey}] PRIMARY KEY CLUSTERED 
(
	[Destination] ASC
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