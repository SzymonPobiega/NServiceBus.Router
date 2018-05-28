using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

class NativePublishForwarder : IPublishForwarder
{
    RuntimeTypeGenerator typeGenerator;

    public NativePublishForwarder(RuntimeTypeGenerator typeGenerator)
    {
        this.typeGenerator = typeGenerator;
    }

    public Task Forward(MessageContext context, IRawEndpoint dispatcher)
    {
        if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
        {
            throw new UnforwardableMessageException("Message need to have 'NServiceBus.EnclosedMessageTypes' header in order to be routed.");
        }
        var types = messageTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        var addressTag = new MulticastAddressTag(typeGenerator.GetType(types.First()));
        var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
        var operation = new TransportOperation(outgoingMessage, addressTag);

        return dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions);
    }
}