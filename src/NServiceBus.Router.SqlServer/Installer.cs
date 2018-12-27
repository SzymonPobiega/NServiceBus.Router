namespace NServiceBus.Router.Deduplication
{
    using System.Threading.Tasks;
    using Inbox;
    using Outbox;

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

        public async Task Start(RootContext rootContext, SettingsHolder extensibilitySettings)
        {
            if (settings.Uninstall)
            {
                foreach (var link in settings.Links)
                {
                    using (var conn = link.Value.ConnectionFactory())
                    {
                        await conn.OpenAsync().ConfigureAwait(false);

                        using (var trans = conn.BeginTransaction())
                        {
                            await outboxInstaller.Uninstall(link.Key, conn, trans).ConfigureAwait(false);
                            await inboxInstaller.Uninstall(link.Key, conn, trans).ConfigureAwait(false);
                            trans.Commit();
                        }
                    }
                }
            }

            foreach (var link in settings.Links)
            {
                using (var conn = link.Value.ConnectionFactory())
                {
                    await conn.OpenAsync().ConfigureAwait(false);

                    using (var trans = conn.BeginTransaction())
                    {
                        await outboxInstaller.Install(link.Key, conn, trans).ConfigureAwait(false);
                        await inboxInstaller.Install(link.Key, conn, trans).ConfigureAwait(false);
                        trans.Commit();
                    }
                }
            }
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }
    }
}