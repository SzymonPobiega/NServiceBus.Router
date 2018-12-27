namespace NServiceBus.Router
{
    using Raw;
    using Routing;
    using Settings;

    /// <summary>
    /// Defines the context for creating instances of rules and determining if a given rule should be attached to a chain.
    /// </summary>
    public interface IRuleCreationContext
    {
        /// <summary>
        /// Name of the interface.
        /// </summary>
        string InterfaceName { get; }
        /// <summary>
        /// The endpoint instance collection for a given interface.
        /// </summary>
        EndpointInstances EndpointInstances { get; }

        /// <summary>
        /// The distribution policy configured for a given interface..
        /// </summary>
        RawDistributionPolicy DistributionPolicy { get; }

        /// <summary>
        /// The endpoint associated with a given interface.
        /// </summary>
        IRawEndpoint Endpoint { get; }

        /// <summary>
        /// The type generator used to create type objects from message type strings.
        /// </summary>
        RuntimeTypeGenerator TypeGenerator { get; }

        /// <summary>
        /// Settings for the interface merged with router-wide settings.
        /// </summary>
        ReadOnlySettings Settings { get; }
    }
}