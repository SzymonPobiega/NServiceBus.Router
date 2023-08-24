namespace NServiceBus.Router
{
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    /// <summary>
    /// Configures message-driven pub/sub
    /// </summary>
    public static class MessageDrivenSubscriptionExtensions
    {
        /// <summary>
        /// Enables message-driven storage-based publish/subscribe for a given interface
        /// </summary>
        /// <param name="interfaceConfig">Interface configuration.</param>
        /// <param name="subscriptionStorage">Subscription storage.</param>
        public static void EnableMessageDrivenPublishSubscribe(this InterfaceConfiguration interfaceConfig, ISubscriptionStorage subscriptionStorage)
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", true);
            interfaceConfig.Settings.Set<ISubscriptionStorage>(subscriptionStorage);
        }

        /// <summary>
        /// Disables message-driven storage-based publish/subscribe for a given interface. 
        /// </summary>
        /// <param name="interfaceConfig"></param>
        public static void DisableMessageDrivenPublishSubscribe(this InterfaceConfiguration interfaceConfig)
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", false);
        }

        /// <summary>
        /// Enables message-driven storage-based publish/subscribe for a given send-only interface.
        /// </summary>
        /// <param name="interfaceConfig">Send-only interface configuration.</param>
        /// <param name="subscriptionStorage">Subscription storage.</param>
        public static void EnableMessageDrivenPublishSubscribe(this SendOnlyInterfaceConfiguration interfaceConfig, ISubscriptionStorage subscriptionStorage)
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", true);
            interfaceConfig.Settings.Set<ISubscriptionStorage>(subscriptionStorage);
        }

        /// <summary>
        /// Disables message-driven storage-based publish/subscribe for a given send-only interface.
        /// </summary>
        /// <param name="interfaceConfig">Send-only interface configuration.</param>
        public static void DisableMessageDrivenPublishSubscribe(this SendOnlyInterfaceConfiguration interfaceConfig)
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", false);
        }
    }
}