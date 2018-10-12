namespace NServiceBus.Router
{
    using System;
    using System.Threading.Tasks;

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
            MessageId = parentContext.MessageId;
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

        /// <summary>
        /// The ID of the received message.
        /// </summary>
        public string MessageId { get; }
    }

    class ForwardPublishTerminator : ChainTerminator<ForwardPublishContext>
    {
        protected override Task Terminate(ForwardPublishContext context)
        {
            return Task.CompletedTask;
        }
    }
}