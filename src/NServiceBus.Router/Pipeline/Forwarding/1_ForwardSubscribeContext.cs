namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;

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
        /// The logical name of the endpoint which sent the subscribe request.
        /// </summary>
        public string SubscriberEndpoint { get; }

        /// <summary>
        /// The physical address of the endpoint which sent the unsubscribe request.
        /// </summary>
        public string SubscriberAddress { get; }

        /// <summary>
        /// Instance of event type to subscribe.
        /// </summary>
        public Type MessageRuntimeType { get; }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardSubscribeContext(string outgoingInterface, Route[] routes, Type runtimeType, string subscriberEndpoint, string subscriberAddress, SubscribePreroutingContext parentContext) 
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            MessageType = parentContext.MessageType;
            MessageRuntimeType = runtimeType;
            SubscriberEndpoint = subscriberEndpoint;
            SubscriberAddress = subscriberAddress;
        }
    }
}