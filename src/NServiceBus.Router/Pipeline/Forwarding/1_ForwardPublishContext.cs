namespace NServiceBus.Router
{
    using System;

    /// <summary>
    /// Defines the context for the forward publish chain.
    /// </summary>
    public class ForwardPublishContext : BaseForwardRuleContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardPublishContext(string outgoingInterface, Type rootType, PublishPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
            Types = parentContext.Types;
            RootEventType = rootType;
        }

        /// <summary>
        /// Event types associated with the message being forwarded.
        /// </summary>
        public string[] Types { get; }

        /// <summary>
        /// Root event type.
        /// </summary>
        public Type RootEventType { get; }

        /// <summary>
        /// The headers associated with the received message.
        /// </summary>
        public IReceivedMessageHeaders ReceivedHeaders { get; }

        /// <summary>
        /// The headers associated with the received message.
        /// </summary>
        public byte[] ReceivedBody { get; }
    }
}