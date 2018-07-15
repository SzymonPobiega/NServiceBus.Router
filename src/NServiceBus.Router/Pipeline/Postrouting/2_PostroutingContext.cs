namespace NServiceBus.Router
{
    using Transport;

    /// <summary>
    /// Defines the context for the postrouting chain which purpose is to send out messages.
    /// </summary>
    public class PostroutingContext : RuleContext
    {
        /// <summary>
        /// A collection of transport operations to dispatch. Each operation includes the message and its physical destination address.
        /// </summary>
        public TransportOperations Messages { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PostroutingContext(TransportOperations messages, RuleContext parent) : base(parent)
        {
            Messages = messages;
        }
    }
}