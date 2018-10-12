namespace NServiceBus.Router
{
    using System;
    using Deduplication;

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

            var outboxPersistence = new OutboxPersister(settings.EpochSizeValue, routerConfig.Name);
            var inboxPersistence = new InboxPersister(settings.EpochSizeValue, routerConfig.Name);

            var dispatcher = new Dispatcher(settings, outboxPersistence, settings.ConnFactory);
            var outboxCleanerCollection = new OutboxCleanerCollection(settings, outboxPersistence);
            var inboxCleanerCollection = new InboxCleanerCollection(settings, inboxPersistence);

            if (settings.RunInstaller)
            {
                routerConfig.Modules.Add(new Installer(settings, outboxPersistence, inboxPersistence));
            }

            routerConfig.Modules.Add(dispatcher);
            routerConfig.Modules.Add(outboxCleanerCollection);
            routerConfig.Modules.Add(inboxCleanerCollection);

            routerConfig.AddRule(_ => new CaptureOutgoingMessageRule(settings));
            routerConfig.AddRule(_ => new OutboxRule(outboxPersistence, outboxCleanerCollection, dispatcher));
            routerConfig.AddRule(_ => new InboxRule(inboxPersistence, inboxCleanerCollection, settings));

            return settings;
        }
    }
}