namespace NServiceBus.Router.Deduplication
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class OutboxSequence
    {
        string name;

        public OutboxSequence(string sourceKey, string destinationKey)
        {
            name = $"Sequence_{sourceKey}_{destinationKey}";
        }

        public Task Drop(SqlConnection conn, SqlTransaction trans)
        {
            var script = $@"
IF EXISTS (
    SELECT * 
    FROM sys.objects 
    WHERE object_id = OBJECT_ID(N'[dbo].[{name}]') 
        AND type = 'SO')
DROP SEQUENCE [dbo].[{name}]
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
        AND type = 'SO')
RETURN

CREATE SEQUENCE [dbo].[{name}] AS [bigint] START WITH 0 INCREMENT BY 1
";
            using (var command = new SqlCommand(script, conn, trans))
            {
                return command.ExecuteNonQueryAsync();
            }
        }

        public async Task<long> GetNextValue(SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"select next value for [{name}]", conn, trans))
            {
                var value = (long)await command.ExecuteScalarAsync().ConfigureAwait(false);
                return value;
            }
        }
    }
}