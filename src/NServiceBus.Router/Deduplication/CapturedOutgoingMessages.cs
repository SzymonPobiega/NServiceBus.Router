namespace NServiceBus.Router.Deduplication
{
    using System.Collections.Generic;
    using Transport;

    class PersistentOutboxTransportOperation
    {
        public PersistentOutboxTransportOperation(string messageId, Dictionary<string, string> options, byte[] body, Dictionary<string, string> headers)
        {
            MessageId = messageId;
            Options = options;
            Body = body;
            Headers = headers;
        }

        public string MessageId { get; }

        public Dictionary<string, string> Options { get; }

        public byte[] Body { get; }

        public Dictionary<string, string> Headers { get; }
    }

    class CapturedTransportOperation
    {
        public CapturedTransportOperation(OutgoingMessage outgoingMessage, string destination)
        {
            OutgoingMessage = outgoingMessage;
            Destination = destination;
        }

        public void AssignSequence(long sequence)
        {
            Sequence = sequence;
        }

        public OutgoingMessage OutgoingMessage { get; }
        public string Destination { get; }
        public long Sequence { get; private set; }
    }
}