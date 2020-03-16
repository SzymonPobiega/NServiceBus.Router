namespace NServiceBus.Router
{
    using Transport;
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
        public static void EnableMessageDrivenPublishSubscribe<T>(this InterfaceConfiguration<T> interfaceConfig, ISubscriptionStorage subscriptionStorage)
            where T : TransportDefinition, new()
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", true);
            interfaceConfig.Settings.Set<ISubscriptionStorage>(subscriptionStorage);
        }

        /// <summary>
        /// Disables message-driven storage-based publish/subscribe for a given interface. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="interfaceConfig"></param>
        public static void DisableMessageDrivenPublishSubscribe<T>(this InterfaceConfiguration<T> interfaceConfig)
            where T : TransportDefinition, new()
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", false);
        }

        /// <summary>
        /// Enables message-driven storage-based publish/subscribe for a given send-only interface.
        /// </summary>
        /// <param name="interfaceConfig">Send-only interface configuration.</param>
        /// <param name="subscriptionStorage">Subscription storage.</param>
        public static void EnableMessageDrivenPublishSubscribe<T>(this SendOnlyInterfaceConfiguration<T> interfaceConfig, ISubscriptionStorage subscriptionStorage)
            where T : TransportDefinition, new()
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", true);
            interfaceConfig.Settings.Set<ISubscriptionStorage>(subscriptionStorage);
        }

        /// <summary>
        /// Disables message-driven storage-based publish/subscribe for a given send-only interface.
        /// </summary>
        /// <param name="interfaceConfig">Send-only interface configuration.</param>
        public static void DisableMessageDrivenPublishSubscribe<T>(this SendOnlyInterfaceConfiguration<T> interfaceConfig)
            where T : TransportDefinition, new()
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", false);
        }
    }
}