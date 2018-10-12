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
        public TransportOperation[] Messages { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PostroutingContext(TransportOperation message, string @interface, RootContext parent) : base(parent, @interface)
        {
            Messages = new[] { message };
        }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PostroutingContext(TransportOperation message, RuleContext parent) : base(parent)
        {
            Messages = new []{message};
        }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PostroutingContext(TransportOperation[] messages, RuleContext parent) : base(parent)
        {
            Messages = messages;
        }
    }
}