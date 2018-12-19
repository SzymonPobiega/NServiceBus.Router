namespace NServiceBus.Router
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines the context for the third part of the prerouting chain group -- the prerouting chain for subscribe messages.
    /// </summary>
    public class SubscribePreroutingContext : BasePreroutingContext
    {
        /// <summary>
        /// Type of event to subscribe.
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
        /// Creates new instance.
        /// </summary>
        public SubscribePreroutingContext(PreroutingContext parent, string messageType, string subscriberEndpoint, string subscriberAddress) 
            : base(parent)
        {
            MessageType = messageType;
            SubscriberEndpoint = subscriberEndpoint;
            SubscriberAddress = subscriberAddress;
            Destinations = new List<Destination>();
        }

        /// <summary>
        /// The destinations for the received message. Rules in this chain are supposed to add destinations based on the
        /// headers or content of the received message. In order for the message to be forwarded it has to be assigned at
        /// least one destination.
        /// </summary>
        public List<Destination> Destinations { get; }
    }
}