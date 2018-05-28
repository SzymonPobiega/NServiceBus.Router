namespace NServiceBus
{
    using System.Threading.Tasks;
    using Transport;

    class TestTransportQueueCreator : ICreateQueues
    {
        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity) => Task.CompletedTask;
    }
}