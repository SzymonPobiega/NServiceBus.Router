using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Pipeline;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Transport;

class RouterUnsubscribeBehavior : Behavior<IUnsubscribeContext>
{
    public RouterUnsubscribeBehavior(string subscriberAddress, string subscriberEndpoint, string routerAddress, IDispatchMessages dispatcher, Dictionary<Type, string> publisherTable, bool invokeTerminator)
    {
        this.subscriberAddress = subscriberAddress;
        this.subscriberEndpoint = subscriberEndpoint;
        this.routerAddress = routerAddress;
        this.dispatcher = dispatcher;
        this.publisherTable = publisherTable;
        this.invokeTerminator = invokeTerminator;
    }

    public override async Task Invoke(IUnsubscribeContext context, Func<Task> next)
    {
        var eventType = context.EventType;
        if (publisherTable.TryGetValue(eventType, out var publisherEndpoint))
        {
            Logger.Debug($"Sending unsubscribe request for {eventType.AssemblyQualifiedName} to router queue {routerAddress} to be forwarded to {publisherEndpoint}");

            var subscriptionMessage = ControlMessageFactory.Create(MessageIntentEnum.Unsubscribe);

            subscriptionMessage.Headers[Headers.SubscriptionMessageType] = eventType.AssemblyQualifiedName;
            subscriptionMessage.Headers[Headers.ReplyToAddress] = subscriberAddress;
            subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = subscriberAddress;
            subscriptionMessage.Headers[Headers.SubscriberEndpoint] = subscriberEndpoint;
            subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = publisherEndpoint;
            subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
            subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

            var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(routerAddress));
            var transportTransaction = context.Extensions.GetOrCreate<TransportTransaction>();
            await dispatcher.Dispatch(new TransportOperations(transportOperation), transportTransaction, context.Extensions).ConfigureAwait(false);

            if (invokeTerminator)
            {
                await next().ConfigureAwait(false);
            }
        }
        else
        {
            await next().ConfigureAwait(false);
        }
    }

    IDispatchMessages dispatcher;
    Dictionary<Type, string> publisherTable;
    bool invokeTerminator;
    string subscriberAddress;
    string subscriberEndpoint;
    string routerAddress;

    static ILog Logger = LogManager.GetLogger<RouterUnsubscribeBehavior>();
}
