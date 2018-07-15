namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the context for the forward unsubscribe chain.
    /// </summary>
    public class ForwardUnsubscribeContext : BaseForwardRuleContext
    {
        /// <summary>
        /// Routes calculated for the message.
        /// </summary>
        public IReadOnlyCollection<Route> Routes { get; }

        /// <summary>
        /// Type of the event to unsubscribe.
        /// </summary>
        public string MessageType { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardUnsubscribeContext(string outgoingInterface, Route[] routes, UnsubscribePreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            MessageType = parentContext.MessageType;
        }
    }

    class ForwardUnsubscribeTerminator : ChainTerminator<ForwardUnsubscribeContext>
    {
        protected override Task Terminate(ForwardUnsubscribeContext context)
        {
            return Task.CompletedTask;
        }
    }
}