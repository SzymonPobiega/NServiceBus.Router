namespace NServiceBus.Router.Deduplication
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Transport;

    interface IInboxPersitence
    {
        Task<IInboxTransaction> BeginTransaction();
        Task<bool> Store(string messageId, byte[] body, IReadOnlyDictionary<string, string> headers, TransportTransaction transportTransaction);
    }
}