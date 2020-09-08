using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Pipeline;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Transport;

class RouterSubscribeBehavior : Behavior<ISubscribeContext>
{
    public RouterSubscribeBehavior(string subscriberAddress, string subscriberEndpoint, IDispatchMessages dispatcher, CompiledRouterConnectionSettings compiledSettings, bool nativePubSub)
    {
        this.subscriberAddress = subscriberAddress;
        this.subscriberEndpoint = subscriberEndpoint;
        this.dispatcher = dispatcher;
        this.compiledSettings = compiledSettings;
        this.nativePubSub = nativePubSub;
    }

    public override async Task Invoke(ISubscribeContext context, Func<Task> next)
    {
        var eventType = context.EventType;
        if (compiledSettings.TryGetPublisher(eventType, out var publisherInfo))
        {
            Logger.Debug($"Sending subscribe request for {eventType.AssemblyQualifiedName} to router queue {publisherInfo.Router} to be forwarded to {publisherInfo.Endpoint}");

            var subscriptionMessage = ControlMessageFactory.Create(MessageIntentEnum.Subscribe);

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

            if (nativePubSub)
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
    bool nativePubSub;
    string subscriberAddress;
    string subscriberEndpoint;

    static ILog Logger = LogManager.GetLogger<RouterSubscribeBehavior>();
}
