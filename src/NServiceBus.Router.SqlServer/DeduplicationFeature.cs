namespace NServiceBus.Router
{
    using Deduplication;
    using Deduplication.Inbox;
    using Deduplication.Outbox;

    class DeduplicationFeature : IFeature
    {
        public void Configure(RouterConfiguration routerConfig)
        {
            var settings = routerConfig.Settings.Get<DeduplicationSettings>();

            var inboxDestinationKey = routerConfig.Name;
            var outboxSourceKey = routerConfig.Name;

            var inboxInstaller = new InboxInstaller(inboxDestinationKey);
            var inboxPersisterCollection = new InboxPersisterCollection(inboxDestinationKey, settings);

            var outboxInstaller = new OutboxInstaller(outboxSourceKey);
            var outboxPersisterCollection = new OutboxPersisterCollection(outboxSourceKey, settings);

            var dispatcher = new Dispatcher(settings);

            if (settings.RunInstaller)
            {
                routerConfig.AddModule(new Installer(settings, outboxInstaller, inboxInstaller));
            }

            routerConfig.AddModule(dispatcher);
            routerConfig.AddModule(outboxPersisterCollection);
            routerConfig.AddModule(inboxPersisterCollection);

            routerConfig.AddRule(_ => new CaptureOutgoingMessageRule(settings));
            routerConfig.AddRule(_ => new OutboxRule(outboxPersisterCollection, dispatcher));
            routerConfig.AddRule(_ => new InboxRule(inboxPersisterCollection, settings));
        }
    }
}