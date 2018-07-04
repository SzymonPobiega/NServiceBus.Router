using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

abstract class SubscriptionForwarder
{
    public async Task Forward(string incomingInterface, MessageContext context, MessageIntentEnum intent, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        var messageTypeString = GetSubscriptionMessageTypeFrom(context);

        if (string.IsNullOrEmpty(messageTypeString))
        {
            throw new UnforwardableMessageException("Message intent is Subscribe, but the subscription message type header is missing.");
        }

        string subscriberEndpoint = null;

        context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var publisherEndpoint);

        if (context.Headers.TryGetValue(Headers.SubscriberTransportAddress, out var subscriberAddress))
        {
            subscriberEndpoint = context.Headers[Headers.SubscriberEndpoint];
        }
        else
        {
            subscriberAddress = GetReplyToAddress(context);
        }

        if (subscriberAddress == null)
        {
            throw new UnforwardableMessageException("Subscription message arrived without a valid ReplyToAddress.");
        }

        var subscriber = new Subscriber(subscriberAddress, subscriberEndpoint);
        if (intent == MessageIntentEnum.Subscribe)
        {
            await ForwardSubscribe(incomingInterface, context, subscriber, publisherEndpoint, messageTypeString, dispatcher, routeTable).ConfigureAwait(false);
        }
        else
        {
            await ForwardUnsubscribe(incomingInterface, context, subscriber, publisherEndpoint, messageTypeString, dispatcher, routeTable).ConfigureAwait(false);
        }
    }

    static string GetSubscriptionMessageTypeFrom(MessageContext msg)
    {
        msg.Headers.TryGetValue(Headers.SubscriptionMessageType, out var value);
        return value;
    }

    static string GetReplyToAddress(MessageContext message)
    {
        return message.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress) ? replyToAddress : null;
    }

    public abstract Task ForwardSubscribe(string incomingInterface, MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, RouteTable routeTable);
    public abstract Task ForwardUnsubscribe(string incomingInterface, MessageContext context, Subscriber subscriber, string publisherEndpoint, string messageType, IRawEndpoint dispatcher, RouteTable routeTable);
}