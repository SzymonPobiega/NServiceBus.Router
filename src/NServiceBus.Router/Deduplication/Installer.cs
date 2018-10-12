using System.Threading.Tasks;

namespace NServiceBus.Router.Deduplication
{
    class Installer : IModule
    {
        DeduplicationSettings settings;
        OutboxPersister outboxPersister;
        InboxPersister inboxPersister;

        public Installer(DeduplicationSettings settings, OutboxPersister outboxPersister, InboxPersister inboxPersister)
        {
            this.settings = settings;
            this.outboxPersister = outboxPersister;
            this.inboxPersister = inboxPersister;
        }

        public async Task Start(RootContext rootContext)
        {
            if (settings.Uninstall)
            {
                using (var conn = settings.ConnFactory())
                {
                    await conn.OpenAsync().ConfigureAwait(false);

                    using (var trans = conn.BeginTransaction())
                    {
                        foreach (var destination in settings.GetAllDestinations())
                        {
                            await outboxPersister.Uninstall(destination, conn, trans).ConfigureAwait(false);
                        }
                        foreach (var source in settings.GetAllSources())
                        {
                            await inboxPersister.Uninstall(source, conn, trans).ConfigureAwait(false);
                        }
                        trans.Commit();
                    }
                }
            }

            using (var conn = settings.ConnFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);

                using (var trans = conn.BeginTransaction())
                {
                    foreach (var destination in settings.GetAllDestinations())
                    {
                        await outboxPersister.Install(destination, conn, trans).ConfigureAwait(false);
                    }
                    foreach (var source in settings.GetAllSources())
                    {
                        await inboxPersister.Install(source, conn, trans).ConfigureAwait(false);
                    }
                    trans.Commit();
                }
            }
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }
    }
}