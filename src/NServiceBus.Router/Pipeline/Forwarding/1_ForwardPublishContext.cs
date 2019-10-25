namespace NServiceBus.Router
{
    using System;

    /// <summary>
    /// Defines the context for the forward publish chain.
    /// </summary>
    public class ForwardPublishContext : BaseForwardRuleContext
    {
        internal bool Forwarded;
        internal bool Dropped;

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardPublishContext(string outgoingInterface, Type rootType, Action acknowledgeMessageDrop, PublishPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            this.acknowledgeMessageDrop = acknowledgeMessageDrop;
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
            Types = parentContext.Types;
            RootEventType = rootType;
        }

        /// <summary>
        /// Marks this message as OK to be dropped if no chain terminator forwards it.
        /// </summary>
        public void DoNotRequireThisMessageToBeForwarded()
        {
            acknowledgeMessageDrop();
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
        /// Mark this message as forwarded.
        /// </summary>
        /// <returns></returns>
        public void MarkForwarded()
        {
            Forwarded = true;
        }

        /// <summary>
        /// Marks this message as OK to be dropped if no chain terminator forwards it.
        /// </summary>
        public void DoNotRequireThisMessageToBeForwarded()
        {
            Dropped = true;
        }
    }
}