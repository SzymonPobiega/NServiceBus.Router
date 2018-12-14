using System.Threading.Tasks;

namespace NServiceBus.Router.Deduplication
{
    using Inbox;

    class Installer : IModule
    {
        DeduplicationSettings settings;
        OutboxInstaller outboxInstaller;
        InboxInstaller inboxInstaller;

        public Installer(DeduplicationSettings settings, OutboxInstaller outboxInstaller, InboxInstaller inboxInstaller)
        {
            this.settings = settings;
            this.outboxInstaller = outboxInstaller;
            this.inboxInstaller = inboxInstaller;
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
                            await outboxInstaller.Uninstall(destination, conn, trans).ConfigureAwait(false);
                        }
                        foreach (var source in settings.GetAllSources())
                        {
                            await inboxInstaller.Uninstall(source, conn, trans).ConfigureAwait(false);
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
                        await outboxInstaller.Install(destination, conn, trans).ConfigureAwait(false);
                    }
                    foreach (var source in settings.GetAllSources())
                    {
                        await inboxInstaller.Install(source, conn, trans).ConfigureAwait(false);
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