namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the context for the third part of the prerouting chain group -- the prerouting chain for send messages.
    /// </summary>
    public class SendPreroutingContext : BasePreroutingContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        public SendPreroutingContext(PreroutingContext parent) : base(parent)
        {
            Body = parent.Body;
            Destinations = new List<Destination>();
        }

        /// <summary>
        /// The destinations for the received message. Rules in this chain are supposed to add destinations based on the
        /// headers or content of the received message. In order for the message to be forwarded it has to be assigned at
        /// least one destination.
        /// </summary>
        public List<Destination> Destinations { get; }

        /// <summary>
        /// The body of the received message.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }
    }
}