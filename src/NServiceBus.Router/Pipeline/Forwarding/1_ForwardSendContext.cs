namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the context for the forward send chain.
    /// </summary>
    public class ForwardSendContext : BaseForwardRuleContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardSendContext(string outgoingInterface, Route[] routes, SendPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
            MessageId = parentContext.MessageId;
        }

        /// <summary>
        /// The routes calculated for the message.
        /// </summary>
        public IReadOnlyCollection<Route> Routes { get; }

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

    class ForwardSendTerminator : ChainTerminator<ForwardSendContext>
    {
        protected override Task Terminate(ForwardSendContext context)
        {
            return Task.CompletedTask;
        }
    }
}