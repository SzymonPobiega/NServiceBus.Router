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

            var outboxPersistence = new OutboxPersistence(settings.EpochSizeValue);

            var dispatcher = new Dispatcher(settings, outboxPersistence, settings.ConnFactory);
            var epochManager = new EpochManager(settings, outboxPersistence, settings.ConnFactory);

            routerConfig.Modules.Add(new Installer(settings, outboxPersistence, settings.ConnFactory));
            routerConfig.Modules.Add(dispatcher);
            routerConfig.Modules.Add(epochManager);

            routerConfig.AddRule(_ => new CaptureOutgoingMessageRule(settings));
            routerConfig.AddRule(_ => new OutboxRule(outboxPersistence, epochManager, dispatcher));

            return settings;
        }
    }
}