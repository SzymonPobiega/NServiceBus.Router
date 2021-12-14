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
        compiledSettings.TryGetPublisher(eventType, out var publisherInfo);

        //Router auto-subscribe
        foreach (var router in compiledSettings.AutoSubscribeRouters)
        {
            if (publisherInfo != null && publisherInfo.Router == router)
            {
                //We have an explicit publisher registration for this router
                continue;
            }
            Logger.Debug($"Sending subscribe request for {eventType.AssemblyQualifiedName} to router queue {router}.");
            await SendSubscribeMessage(context, eventType, null, router).ConfigureAwait(false);
        }

        if (publisherInfo != null)
        {
            Logger.Debug($"Sending subscribe request for {eventType.AssemblyQualifiedName} to router queue {publisherInfo.Router} to be forwarded to {publisherInfo.Endpoint}");

            await SendSubscribeMessage(context, eventType, publisherInfo.Endpoint, publisherInfo.Router).ConfigureAwait(false);

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

    async Task SendSubscribeMessage(ISubscribeContext context, Type eventType, string publisherEndpoint, string router)
    {
        var subscriptionMessage = ControlMessageFactory.Create(MessageIntentEnum.Subscribe);

        subscriptionMessage.Headers[Headers.SubscriptionMessageType] = eventType.AssemblyQualifiedName;
        subscriptionMessage.Headers[Headers.ReplyToAddress] = subscriberAddress;
        subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = subscriberAddress;
        subscriptionMessage.Headers[Headers.SubscriberEndpoint] = subscriberEndpoint;
        subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
        subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

        if (publisherEndpoint != null)
        {
            subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = publisherEndpoint;
        }

        var transportOperation = new TransportOperation(subscriptionMessage, new UnicastAddressTag(router));
        var transportTransaction = context.Extensions.GetOrCreate<TransportTransaction>();
        await dispatcher.Dispatch(new TransportOperations(transportOperation), transportTransaction, context.Extensions).ConfigureAwait(false);
    }

    IDispatchMessages dispatcher;
    readonly CompiledRouterConnectionSettings compiledSettings;
    bool nativePubSub;
    string subscriberAddress;
    string subscriberEndpoint;

    static ILog Logger = LogManager.GetLogger<RouterSubscribeBehavior>();
}
