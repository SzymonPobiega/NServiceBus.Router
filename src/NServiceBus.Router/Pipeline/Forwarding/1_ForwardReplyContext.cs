namespace NServiceBus.Router
{
    using System;

    /// <summary>
    /// Defines the context for the forward reply chain.
    /// </summary>
    public class ForwardReplyContext : BaseForwardRuleContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardReplyContext(string outgoingInterface, ReplyPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
        }

        /// <summary>
        /// The headers associated with the received message.
        /// </summary>
        public IReceivedMessageHeaders ReceivedHeaders { get; }

        /// <summary>
        /// The headers associated with the received message.
        /// </summary>
        public ReadOnlyMemory<byte> ReceivedBody { get; }
    }
}