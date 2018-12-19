namespace NServiceBus.Router
{
    using System;
    using System.Data.SqlClient;

    /// <summary>
    /// Configures message deduplication based on sequence numbers.
    /// </summary>
    public static class DeduplicationExtensions
    {
        /// <summary>
        /// Defines a de-duplicating link with another router.
        /// </summary>
        /// <param name="iface">This interface.</param>
        /// <param name="outgoingInterface">Interface by which the other router is connected.</param>
        /// <param name="destinationRouter">Address of the other router.</param>
        /// <param name="connectionFactory">Connection factory to use.</param>
        /// <param name="epochSize">Size of an epoch.</param>
        public static void EnableDeduplication(this InterfaceConfiguration<SqlServerTransport> iface, string outgoingInterface, string destinationRouter, Func<SqlConnection> connectionFactory, int epochSize)
        {
            var settings = iface.Settings.GetOrCreate<DeduplicationSettings>();
            settings.EnableLink(iface.Name, outgoingInterface, destinationRouter, connectionFactory, epochSize);

            iface.EnableFeature(typeof(DeduplicationFeature));
        }

        /// <summary>
        /// Configures message deduplication based on sequence numbers.
        /// </summary>
        public static DeduplicationSettings ConfigureDeduplication(this RouterConfiguration routerConfig)
        {
            var settings = routerConfig.Settings.GetOrCreate<DeduplicationSettings>();

            routerConfig.EnableFeature(typeof(DeduplicationFeature));

            return settings;
        }
    }
}