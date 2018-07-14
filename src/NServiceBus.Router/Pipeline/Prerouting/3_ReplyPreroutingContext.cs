namespace NServiceBus.Router
{
    public class ReplyPreroutingContext : BasePreroutingContext
    {
        internal ReplyPreroutingContext(PreroutingContext parent) : base(parent)
        {
            Body = parent.Body;
        }

        public byte[] Body { get; }
    }
}