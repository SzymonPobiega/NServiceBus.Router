namespace NServiceBus.Router
{
    using System.Collections.Generic;

    public class SendPreroutingContext : BasePreroutingContext
    {
        internal SendPreroutingContext(PreroutingContext parent) : base(parent)
        {
            Body = parent.Body;
            Destinations = new List<Destination>();
        }

        public List<Destination> Destinations { get; }
        public byte[] Body { get; }
    }
}