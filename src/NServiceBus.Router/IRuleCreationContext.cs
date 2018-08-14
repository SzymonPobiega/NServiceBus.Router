namespace NServiceBus.Router
{
    using Raw;
    using Routing;
    using Transport;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    /// <summary>
    /// Defines the context for creating instances of rules and determining if a given rule should be attached to a chain.
    /// </summary>
    public interface IRuleCreationContext
    {
        /// <summary>
        /// Name of the interface.
        /// </summary>
        string InetrfaceName { get; }
        /// <summary>
        /// The endpoint instance collection for a given interface.
        /// </summary>
        EndpointInstances EndpointInstances { get; }

        /// <summary>
        /// The subscription persitence configured for a given interface.
        /// </summary>
        ISubscriptionStorage SubscriptionPersistence { get; }

        /// <summary>
        /// The distribution policy configured for a given interface..
        /// </summary>
        RawDistributionPolicy DistributionPolicy { get; }

        /// <summary>
        /// The endpoint associated with a given interface.
        /// </summary>
        IRawEndpoint Endpoint { get; }
    }

    /// <summary>
    /// Provides convenience methods for <seealso cref="IRuleCreationContext"/>.
    /// </summary>
    public static class RuleCreationContextExtensions
    {
        /// <summary>
        /// Returns if a given interface uses transport that has native support for Publish/Subscribe.
        /// </summary>
        public static bool HasNativePubSub(this IRuleCreationContext context)
        {
            var transport = context.Endpoint.Settings.Get<TransportInfrastructure>();
            return transport.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast;
        }
    }
}