namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;

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
        /// Instance of event type to subscribe.
        /// </summary>
        public Type MessageRuntimeType { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardUnsubscribeContext(string outgoingInterface, Route[] routes, Type runtimeType, UnsubscribePreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            MessageType = parentContext.MessageType;
            MessageRuntimeType = runtimeType;
        }
    }
}