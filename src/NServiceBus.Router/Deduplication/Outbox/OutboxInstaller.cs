namespace NServiceBus.Router.Deduplication
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class OutboxInstaller
    {
        string sourceKey;

        public OutboxInstaller(string sourceKey)
        {
            this.sourceKey = sourceKey;
        }

        public async Task Install(string destinationKey, SqlConnection conn, SqlTransaction trans)
        {
            var leftTable = new OutboxTable(OutboxTable.Left(sourceKey, destinationKey));
            var rightTable = new OutboxTable(OutboxTable.Right(sourceKey, destinationKey));

            var sequence = new OutboxSequence(sourceKey, destinationKey);
            var linkStateTable = new LinkStateTable(sourceKey);

            await linkStateTable.Create(conn, trans).ConfigureAwait(false);

            await leftTable.Create(conn, trans).ConfigureAwait(false);
            await rightTable.Create(conn, trans).ConfigureAwait(false);

            await sequence.Create(conn, trans).ConfigureAwait(false);

            await linkStateTable.InitializeLink(destinationKey, conn, trans);
        }

        public async Task Uninstall(string destinationKey, SqlConnection conn, SqlTransaction trans)
        {
            var leftTable = new OutboxTable(OutboxTable.Left(sourceKey, destinationKey));
            var rightTable = new OutboxTable(OutboxTable.Right(sourceKey, destinationKey));

            var sequence = new OutboxSequence(sourceKey, destinationKey);
            var linkStateTable = new LinkStateTable(sourceKey);

            await linkStateTable.Drop(conn, trans).ConfigureAwait(false);

            await leftTable.Drop(conn, trans).ConfigureAwait(false);
            await rightTable.Drop(conn, trans).ConfigureAwait(false);

            await sequence.Drop(conn, trans).ConfigureAwait(false);
        }

    }
}