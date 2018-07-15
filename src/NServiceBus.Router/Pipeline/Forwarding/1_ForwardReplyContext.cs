namespace NServiceBus.Router
{
    using System.Threading.Tasks;

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
            MessageId = parentContext.MessageId;
        }

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

    class ForwardReplyTerminator : ChainTerminator<ForwardReplyContext>
    {
        protected override Task Terminate(ForwardReplyContext context)
        {
            return Task.CompletedTask;
        }
    }
}