namespace NServiceBus.Router.Migrator
{
    using System;
    using Configuration.AdvancedExtensibility;
    using Transport;

    /// <summary>
    /// Configures migrator
    /// </summary>
    public static class MigratorConfigurationExtensions
    {
        internal const string NewTransportCustomizationSettingsKey = "NServiceBus.Router.Migrator.NewTransportCustomization";
        internal const string OldTransportCustomizationSettingsKey = "NServiceBus.Router.Migrator.OldTransportCustomization";

        /// <summary>
        /// Enables transport migration for this endpoint.
        /// </summary>
        /// <typeparam name="TOld">Old transport</typeparam>
        /// <typeparam name="TNew">New transport</typeparam>
        /// <param name="endpointConfiguration">Endpoint configuration</param>
        /// <param name="customizeOldTransport">A callback for customizing the old transport</param>
        /// <param name="customizeNewTransport">A callback for customizing the new transport</param>
        public static MigratorSettings EnableTransportMigration<TOld, TNew>(this EndpointConfiguration endpointConfiguration,
            Action<TransportExtensions<TOld>> customizeOldTransport, Action<TransportExtensions<TNew>> customizeNewTransport)
            where TOld : TransportDefinition, new()
            where TNew : TransportDefinition, new()
        {
            //TODO: Installers

            var settings = endpointConfiguration.GetSettings();
            settings.Set("NServiceBus.Subscriptions.EnableMigrationMode", true);

            var endpointTransport = endpointConfiguration.UseTransport<TNew>();
            customizeNewTransport(endpointTransport);

            settings.Set(NewTransportCustomizationSettingsKey, customizeNewTransport);
            settings.Set(OldTransportCustomizationSettingsKey, customizeOldTransport);

            var setup = new MigratorSetup<TOld, TNew>();
            settings.Set("NServiceBus.Router.Migrator.Setup", setup);
            endpointConfiguration.EnableFeature<MigratorFeature>();

            var migratorSettings = settings.GetOrCreate<MigratorSettings>();
            return migratorSettings;
        }
    }
}