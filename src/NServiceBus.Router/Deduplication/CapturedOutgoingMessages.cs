namespace NServiceBus.Router.Deduplication
{
    using Transport;

    class CapturedOutgoingMessages
    {
        public CapturedOutgoingMessages(string @interface, TransportOperations operations)
        {
            Interface = @interface;
            Operations = operations;
        }

        public string Interface { get; }
        public TransportOperations Operations { get; }
    }
}