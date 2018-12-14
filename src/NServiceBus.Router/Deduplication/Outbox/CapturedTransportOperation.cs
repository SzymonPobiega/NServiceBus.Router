namespace NServiceBus.Router.Deduplication
{
    using Transport;

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

        public void AssignTable(string tableName)
        {
            Table = tableName;
        }

        public OutgoingMessage OutgoingMessage { get; }
        public string Destination { get; }
        public long Sequence { get; private set; }
        public string Table { get; private set; }
    }
}