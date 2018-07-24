using System.Threading.Tasks;

namespace NServiceBus.Router.Deduplication
{
    using Transport;

    interface IInboxTransaction
    {
        TransportTransaction TransportTransaction { get; }
        Task Commit();
        Task Rollback();
    }
}
