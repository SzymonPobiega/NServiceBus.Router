namespace NServiceBus.Router
{
    /// <summary>
    /// Defines the context for the third part of the prerouting chain group -- the prerouting chain for publish messages.
    /// </summary>
    public class PublishPreroutingContext : BasePreroutingContext
    {
        internal PublishPreroutingContext(string[] types, PreroutingContext parent) : base(parent)
        {
            Types = types;
            Body = parent.Body;
        }
        public string[] Types { get; }

        /// <summary>
        /// The body of the received message.
        /// </summary>
        public byte[] Body { get; }
    }
}