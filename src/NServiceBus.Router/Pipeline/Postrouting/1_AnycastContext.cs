namespace NServiceBus.Router
{
    using Transport;

    public class AnycastContext : RuleContext
    {
        public string DestinationEndpoint { get; }
        public OutgoingMessage Message { get; }
        public DistributionStrategyScope DistributionScope { get; }

        public AnycastContext(string destinationEndpoint, OutgoingMessage message, DistributionStrategyScope distributionScope, BaseForwardRuleContext parentContext) : base(parentContext)
        {
            DestinationEndpoint = destinationEndpoint;
            Message = message;
            DistributionScope = distributionScope;
        }
    }
}