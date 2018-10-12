using System.Threading.Tasks;

namespace NServiceBus.Router.Resubscriber
{
    interface IResubscriberStorage
    {
        Task Subscribe(string messageType);
        Task Unsubscribe(string messageType);
    }
}