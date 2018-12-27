using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

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
            interfaceConfig.AddRule(c => new ForwardPublishStorageDrivenRule(subscriptionStorage, c.DistributionPolicy));
            interfaceConfig.AddRule(c => new ForwardSubscribeMessageDrivenRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));
            interfaceConfig.AddRule(c => new ForwardUnsubscribeMessageDrivenRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));
            interfaceConfig.AddRule(c => new StorageDrivenSubscriptionRule(subscriptionStorage));
        }
    }
}
