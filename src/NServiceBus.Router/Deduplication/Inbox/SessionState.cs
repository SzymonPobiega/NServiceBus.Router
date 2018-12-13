namespace NServiceBus.Router.Deduplication.Inbox
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class SessionState
    {
        public long Lo { get; }
        public long Hi { get; }
        public string Table { get; }

        public SessionState(long lo, long hi, string table)
        {
            Lo = lo;
            Hi = hi;
            Table = table;
        }

        public bool Matches(long sequence)
        {
            return sequence >= Lo && sequence < Hi;
        }

        public Task CreateConstraint(SqlConnection conn, SqlTransaction trans)
        {
            var table = new OutboxTable(Table);
            return table.CreateConstraint(Lo, Hi, conn, trans);
        }
    }
}