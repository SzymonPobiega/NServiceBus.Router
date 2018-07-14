namespace NServiceBus.Router
{
    using Transport;

    class PostroutingContext : RuleContext
    {
        public TransportOperations Messages { get; }

        public PostroutingContext(TransportOperations messages, RuleContext parent) : base(parent)
        {
            Messages = messages;
        }
    }
}