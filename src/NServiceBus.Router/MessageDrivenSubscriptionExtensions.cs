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
        public static void EnableMessageDrivenPublishSubscribe(this IInterfaceConfiguration interfaceConfig, ISubscriptionStorage subscriptionStorage)
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", true);
            interfaceConfig.Settings.Set<ISubscriptionStorage>(subscriptionStorage);
        }

        /// <summary>
        /// Disables message-driven storage-based publish/subscribe for a given interface. 
        /// </summary>
        /// <param name="interfaceConfig"></param>
        public static void DisableMessageDrivenPublishSubscribe(this IInterfaceConfiguration interfaceConfig)
        {
            interfaceConfig.Settings.Set("EnableMessageDrivenPubSub", false);
        }
    }
}