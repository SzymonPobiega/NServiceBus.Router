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
            var lo = $"Inbox_{sourceKey}_{destinationKey}_1";
            var hi = $"Inbox_{sourceKey}_{destinationKey}_2";

            var linkStateTable = new LinkStateTable(destinationKey);
            await linkStateTable.Create(conn, trans).ConfigureAwait(false);

            await new InboxTable(lo).Create(conn, trans).ConfigureAwait(false);
            await new InboxTable(hi).Create(conn, trans).ConfigureAwait(false);

            await linkStateTable.InitializeLink(sourceKey, conn, trans).ConfigureAwait(false);
        }

        public async Task Uninstall(string sourceSequenceKey, SqlConnection conn, SqlTransaction trans)
        {
            var lo = $"Inbox_{sourceSequenceKey}_{destinationKey}_1";
            var hi = $"Inbox_{sourceSequenceKey}_{destinationKey}_2";

            var linkStateTable = new LinkStateTable(destinationKey);
            await linkStateTable.Drop(conn, trans).ConfigureAwait(false);

            await new InboxTable(lo).Create(conn, trans).ConfigureAwait(false);
            await new InboxTable(hi).Create(conn, trans).ConfigureAwait(false);
        }
    }
}