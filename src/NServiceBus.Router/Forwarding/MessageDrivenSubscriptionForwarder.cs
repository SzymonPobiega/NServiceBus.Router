using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using NServiceBus.Unicast.Transport;

class MessageDrivenSubscriptionForwarder : SubscriptionForwarder
{
    static ILog Logger = LogManager.GetLogger<MessageDrivenSubscriptionForwarder>();

    EndpointInstances endpointInstances;

    public MessageDrivenSubscriptionForwarder(EndpointInstances endpointInstances)
    {
        this.endpointInstances = endpointInstances;
    }

    public override Task ForwardSubscribe(string incomingInterface, MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        return Send(incomingInterface, context, subscriber, messageType, MessageIntentEnum.Subscribe, dispatcher, routeTable);
    }

    public override Task ForwardUnsubscribe(string incomingInterface, MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        return Send(incomingInterface, context, subscriber, messageType, MessageIntentEnum.Unsubscribe, dispatcher, routeTable);
    }

    async Task Send(string incomingInterface, MessageContext context, Subscriber subscriber, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        var routes = routeTable.Route(incomingInterface, context);

        IEnumerable<TransportOperation> Forward(Route r)
        {
            return r.Gateway != null
                ? Subscribe(subscriber, r.Gateway, messageType, intent, dispatcher, r.Destination)
                : Subscribe(subscriber, r.Destination, messageType, intent, dispatcher, null);
        }

        var ops = routes.SelectMany(Forward).ToArray();

        await dispatcher.Dispatch(new TransportOperations(ops), new TransportTransaction(), new ContextBag()).ConfigureAwait(false);
    }

    IEnumerable<TransportOperation> Subscribe(Subscriber subscriber, string destination, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher, string ultimateDestination)
    {
        var subscriptionMessage = ControlMessageFactory.Create(intent);

        subscriptionMessage.Headers[Headers.SubscriptionMessageType] = messageType;
        subscriptionMessage.Headers[Headers.ReplyToAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = dispatcher.TransportAddress;
        subscriptionMessage.Headers[Headers.SubscriberEndpoint] = dispatcher.EndpointName;
        subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
        subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

        if (ultimateDestination != null)
        {
            subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
        }

        var publisherInstances = endpointInstances.FindInstances(destination);
        var publisherAddresses = publisherInstances.Select(i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i))).ToArray();
        foreach (var publisherAddress in publisherAddresses)
        {
            Logger.Debug(
                $"Sending {intent} request for {messageType} to {publisherAddress} on behalf of {subscriber.TransportAddress}.");

            var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(publisherAddress));
            yield return transportOperation;
        }
    }
}
