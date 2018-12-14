namespace NServiceBus.Router
{
    using System;
    using Deduplication;
    using Deduplication.Inbox;
    using Deduplication.Outbox;

    /// <summary>
    /// Configures message deduplication based on sequence numbers.
    /// </summary>
    public static class DeduplicationExtensions
    {
        /// <summary>
        /// Configures message deduplication based on sequence numbers.
        /// </summary>
        /// <returns></returns>
        public static DeduplicationSettings EnableDeduplication(this RouterConfiguration routerConfig, Action<DeduplicationSettings> configAction)
        {
            var settings = new DeduplicationSettings();

            configAction(settings);
            var inboxDestinationKey = routerConfig.Name;
            var outboxSourceKey = routerConfig.Name;

            var inboxInstaller = new InboxInstaller(inboxDestinationKey);
            var inboxPersisterCollection = new InboxPersisterCollection(inboxDestinationKey, settings);

            var outboxInstaller = new OutboxInstaller(outboxSourceKey);
            var outboxPersisterCollection = new OutboxPersisterCollection(outboxSourceKey, settings);

            var dispatcher = new Dispatcher(settings, settings.ConnFactory);

            if (settings.RunInstaller)
            {
                routerConfig.Modules.Add(new Installer(settings, outboxInstaller, inboxInstaller));
            }

            routerConfig.Modules.Add(dispatcher);
            routerConfig.Modules.Add(outboxPersisterCollection);
            routerConfig.Modules.Add(inboxPersisterCollection);

            routerConfig.AddRule(_ => new CaptureOutgoingMessageRule(settings));
            routerConfig.AddRule(_ => new OutboxRule(outboxPersisterCollection, dispatcher));
            routerConfig.AddRule(_ => new InboxRule(inboxPersisterCollection, settings));

            return settings;
        }
    }
}