namespace NServiceBus.Router.Deduplication
{
    using System;

    /// <summary>
    /// Configures message deduplication based on sequence numbers.
    /// </summary>
    public static class SqlDeduplication
    {
        /// <summary>
        /// Configures message deduplication based on sequence numbers.
        /// </summary>
        /// <returns></returns>
        public static SqlDeduplicationSettings EnableSqlDeduplication(this RouterConfiguration routerConfig, Action<SqlDeduplicationSettings> configAction)
        {
            var settings = new SqlDeduplicationSettings();

            configAction(settings);

            var outboxPersistence = new OutboxPersister(settings.EpochSizeValue, routerConfig.Name);
            var inboxPersistence = new InboxPersister(settings.EpochSizeValue, routerConfig.Name);

            var dispatcher = new Dispatcher(settings, outboxPersistence, settings.ConnFactory);
            var outboxCleanerCollection = new OutboxCleanerCollection(settings, outboxPersistence);
            var inboxCleanerCollection = new InboxCleanerCollection(settings, inboxPersistence);

            routerConfig.Modules.Add(new Installer(settings, outboxPersistence, inboxPersistence, settings.ConnFactory));
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