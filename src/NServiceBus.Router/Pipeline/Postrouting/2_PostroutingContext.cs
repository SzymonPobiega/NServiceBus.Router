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
        /// Optional name of logical destination endpoint.
        /// </summary>
        public string DestinationEndpoint { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PostroutingContext(string destinationEndpoint, TransportOperation message, string @interface, RootContext parent) 
            : base(parent, @interface)
        {
            Messages = new[] { message };
            DestinationEndpoint = destinationEndpoint;
        }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PostroutingContext(string destinationEndpoint, TransportOperation message, RuleContext parent) 
            : base(parent)
        {
            Messages = new []{message};
            DestinationEndpoint = destinationEndpoint;
        }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PostroutingContext(string destinationEndpoint, TransportOperation[] messages, RuleContext parent) : base(parent)
        {
            Messages = messages;
            DestinationEndpoint = destinationEndpoint;
        }
    }
}