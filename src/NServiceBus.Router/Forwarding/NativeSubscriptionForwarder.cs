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

class NativeSubscriptionForwarder : SubscriptionForwarder
{
    static ILog Logger = LogManager.GetLogger<NativeSubscriptionForwarder>();

    IManageSubscriptions subscriptionManager;
    RuntimeTypeGenerator typeGenerator;
    EndpointInstances endpointInstances;

    public NativeSubscriptionForwarder(IManageSubscriptions subscriptionManager, RuntimeTypeGenerator typeGenerator, EndpointInstances endpointInstances)
    {
        this.subscriptionManager = subscriptionManager;
        this.typeGenerator = typeGenerator;
        this.endpointInstances = endpointInstances;
    }

    public override async Task ForwardSubscribe(string incomingInterface, MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        var type = typeGenerator.GetType(messageType);
        await subscriptionManager.Subscribe(type, new ContextBag()).ConfigureAwait(false);
        await Send(incomingInterface, context, subscriber, messageType, MessageIntentEnum.Subscribe, dispatcher, routeTable).ConfigureAwait(false);
    }

    public override async Task ForwardUnsubscribe(string incomingInterface, MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        var type = typeGenerator.GetType(messageType);
        await subscriptionManager.Unsubscribe(type, new ContextBag()).ConfigureAwait(false);
        await Send(incomingInterface, context, subscriber, messageType, MessageIntentEnum.Subscribe, dispatcher, routeTable).ConfigureAwait(false);
    }

    async Task Send(string incomingInterface, MessageContext context, Subscriber subscriber, string messageType, MessageIntentEnum intent, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        var destinations = context.Extensions.Get<Destination[]>();
        var routes = routeTable.Route(incomingInterface, destinations);
        var ops = routes
            .Where(r => r.Gateway != null)
            .SelectMany(r => Subscribe(subscriber, r.Gateway, messageType, intent, dispatcher, r.Destination))
            .ToArray();

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