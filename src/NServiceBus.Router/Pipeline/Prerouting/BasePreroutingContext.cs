namespace NServiceBus.Router
{
    /// <summary>
    /// The base context class for all prerouting group chains.
    /// </summary>
    public abstract class BasePreroutingContext : RuleContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        protected BasePreroutingContext(BasePreroutingContext parentContext) : base(parentContext)
        {
            IncomingInterface = parentContext.IncomingInterface;
            MessageId = parentContext.MessageId;
            Headers = parentContext.Headers;
        }

        internal BasePreroutingContext(RootContext parentContext, string @interface, IReceivedMessageHeaders headers, string messageId) : base(parentContext, @interface)
        {
            MessageId = messageId;
            IncomingInterface = @interface;
            Headers = headers;
        }

        /// <summary>
        /// The headers associated with the received message.
        /// </summary>
        public IReceivedMessageHeaders Headers { get; }

        /// <summary>
        /// The interface on which the message has been received.
        /// </summary>
        public string IncomingInterface { get; }

        /// <summary>
        /// The ID of the received message.
        /// </summary>
        public string MessageId { get; }
    }
}