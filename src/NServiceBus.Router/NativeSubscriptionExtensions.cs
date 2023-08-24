namespace NServiceBus.Router
{
    using Transport;

    /// <summary>
    /// Configures native pub/sub
    /// </summary>
    public static class NativeSubscriptionExtensions
    {
        /// <summary>
        /// Disables native publish/subscribe handling for a given interface.
        /// </summary>
        public static void DisableNativePubSub<T>(this InterfaceConfiguration interfaceConfig)
            where T : TransportDefinition, new()
        {
            interfaceConfig.Settings.Set("NativePubSubDisabled", true);
        }

        /// <summary>
        /// Disables native publish/subscribe handling for a given send-only interface.
        /// </summary>
        public static void DisableNativePubSub<T>(this SendOnlyInterfaceConfiguration interfaceConfig)
            where T : TransportDefinition, new()
        {
            interfaceConfig.Settings.Set("NativePubSubDisabled", true);
        }
    }
}