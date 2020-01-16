namespace NServiceBus
{
    using System.IO;
    using System.Threading.Tasks;
    using Transport;

    class TestTransportQueueCreator : ICreateQueues
    {
        readonly string storagePath;

        public TestTransportQueueCreator(string storagePath)
        {
            this.storagePath = storagePath;
        }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            foreach (var queueBinding in queueBindings.ReceivingAddresses)
            {
                var queuePath = Path.Combine(storagePath, queueBinding);
                Directory.CreateDirectory(queuePath);
            }

            foreach (var queueBinding in queueBindings.SendingAddresses)
            {
                var queuePath = Path.Combine(storagePath, queueBinding);
                Directory.CreateDirectory(queuePath);
            }

            return Task.CompletedTask;
        }
    }
}