namespace NServiceBus.Router
{
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
        /// Creates new instance.
        /// </summary>
        protected BaseForwardRuleContext(string outgoingInterface, RuleContext parentContext) 
            : base(parentContext, outgoingInterface)
        {
            IncomingInterface = parentContext.Interface;
            OutgoingInterface = outgoingInterface;
        }
    }
}