namespace NServiceBus.Router
{
    using System;
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
        /// Instance of event type to subscribe.
        /// </summary>
        public Type MessageRuntimeType { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardSubscribeContext(string outgoingInterface, Route[] routes, Type runtimeType, SubscribePreroutingContext parentContext) 
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            MessageType = parentContext.MessageType;
            MessageRuntimeType = runtimeType;
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