namespace NServiceBus.Router
{
    using System.Collections.Generic;

    public class UnsubscribePreroutingContext : BasePreroutingContext
    {
        public string MessageType { get; }
        public string SubscriberEndpoint { get; }
        public string SubscriberAddress { get; }

        public UnsubscribePreroutingContext(PreroutingContext parent, string messageType, string subscriberEndpoint, string subscriberAddress) 
            : base(parent)
        {
            MessageType = messageType;
            SubscriberEndpoint = subscriberEndpoint;
            SubscriberAddress = subscriberAddress;
            Destinations = new List<Destination>();
        }

        public List<Destination> Destinations { get; }
    }
}