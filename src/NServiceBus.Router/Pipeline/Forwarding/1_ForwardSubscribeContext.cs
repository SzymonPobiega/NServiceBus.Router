namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the context for the forward subscribe chain.
    /// </summary>
    public class ForwardSubscribeContext : BaseForwardRuleContext
    {
        /// <summary>
        /// Routes calculated for the message.
        /// </summary>
        public IReadOnlyCollection<Route> Routes { get; }

        /// <summary>
        /// Type of the event to subscribe.
        /// </summary>
        public string MessageType { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardSubscribeContext(string outgoingInterface, Route[] routes, SubscribePreroutingContext parentContext) 
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            MessageType = parentContext.MessageType;
        }
    }

    class ForwardSubscribeTerminator : ChainTerminator<ForwardSubscribeContext>
    {
        protected override Task Terminate(ForwardSubscribeContext context)
        {
            return Task.CompletedTask;
        }
    }
}