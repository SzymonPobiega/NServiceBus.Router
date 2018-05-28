using System.Threading.Tasks;
using NServiceBus.Raw;
using NServiceBus.Transport;

interface IPublishForwarder
{
    Task Forward(MessageContext context, IRawEndpoint dispatcher);
}
