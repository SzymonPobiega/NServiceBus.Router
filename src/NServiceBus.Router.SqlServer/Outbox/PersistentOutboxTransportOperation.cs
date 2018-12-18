namespace NServiceBus.Router.Deduplication.Outbox
{
    using System.Collections.Generic;

    class PersistentOutboxTransportOperation
    {
        public PersistentOutboxTransportOperation(string messageId, byte[] body, Dictionary<string, string> headers)
        {
            MessageId = messageId;
            Body = body;
            Headers = headers;
        }

        public string MessageId { get; }

        public byte[] Body { get; }

        public Dictionary<string, string> Headers { get; }
    }
}