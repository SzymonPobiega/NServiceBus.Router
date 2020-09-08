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
    public RouterUnsubscribeBehavior(string subscriberAddress, string subscriberEndpoint, IDispatchMessages dispatcher, CompiledRouterConnectionSettings compiledSettings, bool invokeTerminator)
    {
        this.subscriberAddress = subscriberAddress;
        this.subscriberEndpoint = subscriberEndpoint;
        this.dispatcher = dispatcher;
        this.compiledSettings = compiledSettings;
        this.invokeTerminator = invokeTerminator;
    }

    public override async Task Invoke(IUnsubscribeContext context, Func<Task> next)
    {
        var eventType = context.EventType;
        if (compiledSettings.TryGetPublisher(eventType, out var publisherInfo))
        {
            Logger.Debug($"Sending unsubscribe request for {eventType.AssemblyQualifiedName} to router queue {publisherInfo.Router} to be forwarded to {publisherInfo.Endpoint}");

            var subscriptionMessage = ControlMessageFactory.Create(MessageIntentEnum.Unsubscribe);

            subscriptionMessage.Headers[Headers.SubscriptionMessageType] = eventType.AssemblyQualifiedName;
            subscriptionMessage.Headers[Headers.ReplyToAddress] = subscriberAddress;
            subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = subscriberAddress;
            subscriptionMessage.Headers[Headers.SubscriberEndpoint] = subscriberEndpoint;
            subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = publisherInfo.Endpoint;
            subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
            subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

            var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(publisherInfo.Router));
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
    readonly CompiledRouterConnectionSettings compiledSettings;
    bool invokeTerminator;
    string subscriberAddress;
    string subscriberEndpoint;

    static ILog Logger = LogManager.GetLogger<RouterUnsubscribeBehavior>();
}
