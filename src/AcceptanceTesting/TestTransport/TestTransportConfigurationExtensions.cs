namespace NServiceBus
{
    using Configuration.AdvancedExtensibility;

    /// <summary>
    /// Configuration options for the learning transport.
    /// </summary>
    public static class TestTransportConfigurationExtensions
    {
        /// <summary>
        /// Configures the location where message files are stored.
        /// </summary>
        /// <param name="transportExtensions">The transport extensions to extend.</param>
        /// <param name="path">The storage path.</param>
        public static void StorageDirectory(this TransportExtensions<TestTransport> transportExtensions, string path)
        {
            PathChecker.ThrowForBadPath(path, "StorageDirectory");

            transportExtensions.GetSettings().Set(TestTransportInfrastructure.StorageLocationKey, path);
        }

        /// <summary>
        /// Allows messages of any size to be sent.
        /// </summary>
        /// <param name="transportExtensions">The transport extensions to extend.</param>
        public static void NoPayloadSizeRestriction(this TransportExtensions<TestTransport> transportExtensions)
        {
            transportExtensions.GetSettings().Set(TestTransportInfrastructure.NoPayloadSizeRestrictionKey, true);
        }

        /// <summary>
        /// Disables native pub/sub
        /// </summary>
        /// <param name="transportExtensions"></param>
        public static void NoNativePubSub(this TransportExtensions<TestTransport> transportExtensions)
        {
            transportExtensions.GetSettings().Set(TestTransportInfrastructure.NoNativePubSub, true);
        }
    }
}
