namespace NServiceBus.Router
{
    using Transport;

    /// <summary>
    /// Defines the context for the of the anycast chain that leads to the postrouting chain.
    /// The purpose of the multicast chain is to send messages to a single instance of a given logical endpoint.
    /// </summary>
    public class AnycastContext : RuleContext
    {
        /// <summary>
        /// Logical name of the destination endpoint. The message will be sent to a single instance of that endpoint based
        /// on the endpoint instance mapping and the distribution strategy.
        /// </summary>
        public string DestinationEndpoint { get; }

        /// <summary>
        /// The message to be sent.
        /// </summary>
        public OutgoingMessage Message { get; }

        /// <summary>
        /// The distribution scope to use when determining the physical destination address.
        /// </summary>
        public DistributionStrategyScope DistributionScope { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public AnycastContext(string destinationEndpoint, OutgoingMessage message, DistributionStrategyScope distributionScope, BaseForwardRuleContext parentContext) : base(parentContext)
        {
            DestinationEndpoint = destinationEndpoint;
            Message = message;
            DistributionScope = distributionScope;
        }
    }
}