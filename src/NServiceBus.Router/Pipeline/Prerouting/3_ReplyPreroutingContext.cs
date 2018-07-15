namespace NServiceBus.Router
{
    /// <summary>
    /// Defines the context for the third part of the prerouting chain group -- the prerouting chain for reply messages.
    /// </summary>
    public class ReplyPreroutingContext : BasePreroutingContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ReplyPreroutingContext(PreroutingContext parent) : base(parent)
        {
            Body = parent.Body;
        }

        /// <summary>
        /// The body of the received message.
        /// </summary>
        public byte[] Body { get; }
    }
}