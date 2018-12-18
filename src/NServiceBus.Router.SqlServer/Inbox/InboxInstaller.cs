namespace NServiceBus.Router.Deduplication.Inbox
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class InboxInstaller
    {
        string destinationKey;

        public InboxInstaller(string destinationKey)
        {
            this.destinationKey = destinationKey;
        }

        public async Task Install(string sourceKey, SqlConnection conn, SqlTransaction trans)
        {
            var linkStateTable = new LinkStateTable(destinationKey);
            await linkStateTable.Create(conn, trans).ConfigureAwait(false);

            await new InboxTable(InboxTable.Left(sourceKey, destinationKey)).Create(conn, trans).ConfigureAwait(false);
            await new InboxTable(InboxTable.Right(sourceKey, destinationKey)).Create(conn, trans).ConfigureAwait(false);

            await linkStateTable.InitializeLink(sourceKey, conn, trans).ConfigureAwait(false);
        }

        public async Task Uninstall(string sourceKey, SqlConnection conn, SqlTransaction trans)
        {
            var linkStateTable = new LinkStateTable(destinationKey);
            await linkStateTable.Drop(conn, trans).ConfigureAwait(false);

            await new InboxTable(InboxTable.Left(sourceKey, destinationKey)).Drop(conn, trans).ConfigureAwait(false);
            await new InboxTable(InboxTable.Right(sourceKey, destinationKey)).Drop(conn, trans).ConfigureAwait(false);
        }
    }
}