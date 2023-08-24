namespace NServiceBus.Router
{
    using System;

    /// <summary>
    /// Defines the context for the third part of the prerouting chain group -- the prerouting chain for publish messages.
    /// </summary>
    public class PublishPreroutingContext : BasePreroutingContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        public PublishPreroutingContext(string[] types, PreroutingContext parent) : base(parent)
        {
            Types = types;
            Body = parent.Body;
        }

        /// <summary>
        /// Event types associated with the message being forwarded.
        /// </summary>
        public string[] Types { get; }

        /// <summary>
        /// The body of the received message.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }
    }
}