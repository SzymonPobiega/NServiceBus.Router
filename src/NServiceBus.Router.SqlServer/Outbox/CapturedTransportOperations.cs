namespace NServiceBus.Router.Deduplication.Outbox
{
    using System.Collections;
    using System.Collections.Generic;

    class CapturedTransportOperations : IEnumerable<CapturedTransportOperation>
    {
        List<CapturedTransportOperation> ops = new List<CapturedTransportOperation>();

        public CapturedTransportOperations(string sqlInterface)
        {
            SqlInterface = sqlInterface;
        }

        public string SqlInterface { get; }

        public IEnumerator<CapturedTransportOperation> GetEnumerator()
        {
            return ops.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(CapturedTransportOperation capturedTransportOperation)
        {
            ops.Add(capturedTransportOperation);
        }
    }
}