namespace NServiceBus.Router
{
    /// <summary>
    /// Configures native pub/sub
    /// </summary>
    public static class NativeSubscriptionExtensions
    {
        /// <summary>
        /// Disables native publish/subscribe handling for a given interface.
        /// </summary>
        public static void DisableNativePubSub(this IInterfaceConfiguration interfaceConfig)
        {
            interfaceConfig.Settings.Set("NativePubSubDisabled", true);
        }
    }
}