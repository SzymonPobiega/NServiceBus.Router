namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using Transport;

    /// <summary>
    /// The base context class for all forwarding group chains.
    /// </summary>
    public abstract class BaseForwardRuleContext : RuleContext
    {
        /// <summary>
        /// The outgoing interface calculated by the routing table.
        /// </summary>
        public string OutgoingInterface { get; }

        /// <summary>
        /// The interface on which the message has been received.
        /// </summary>
        public string IncomingInterface { get; }

        /// <summary>
        /// The ID of the received message.
        /// </summary>
        public string MessageId { get; }

        /// <summary>
        /// Headers for the forwarded message.
        /// </summary>
        public Dictionary<string, string> ForwardedHeaders { get; }
        
        /// <summary>
        /// Creates new instance.
        /// </summary>
        protected BaseForwardRuleContext(string outgoingInterface, BasePreroutingContext parentContext) 
            : base(parentContext, outgoingInterface)
        {
            IncomingInterface = parentContext.Interface;
            OutgoingInterface = outgoingInterface;
            MessageId = parentContext.MessageId;
            ForwardedHeaders = parentContext.Headers.Copy();
        }
    }
}