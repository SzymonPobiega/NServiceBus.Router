namespace NServiceBus.Router
{
    using Transport;

    public class MulticastContext : RuleContext
    {
        public string DestinationEndpoint { get; }
        public OutgoingMessage Message { get; }

        public MulticastContext(string destinationEndpoint, OutgoingMessage message, BaseForwardRuleContext parentContext) : base(parentContext)
        {
            DestinationEndpoint = destinationEndpoint;
            Message = message;
        }
    }
}