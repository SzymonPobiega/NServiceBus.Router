using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

abstract class SubscriptionReceiver
{
    public Task Receive(MessageContext context, MessageIntentEnum intent)
    {
        var messageTypeString = GetSubscriptionMessageTypeFrom(context);

        if (string.IsNullOrEmpty(messageTypeString))
        {
            throw new UnforwardableMessageException("Message intent is Subscribe, but the subscription message type header is missing.");
        }

        if (intent != MessageIntentEnum.Subscribe && intent != MessageIntentEnum.Unsubscribe)
        {
            throw new UnforwardableMessageException("Subscription messages need to have intent set to Subscribe/Unsubscribe.");
        }

        string subscriberEndpoint = null;

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
        var messageType = new MessageType(messageTypeString);

        if (intent == MessageIntentEnum.Subscribe)
        {
            return ReceiveSubscribe(subscriber, messageType);
        }
        return ReceiveUnsubscribe(subscriber, messageType);
    }

    static string GetReplyToAddress(MessageContext message)
    {
        return message.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress) ? replyToAddress : null;
    }

    static string GetSubscriptionMessageTypeFrom(MessageContext msg)
    {
        msg.Headers.TryGetValue(Headers.SubscriptionMessageType, out var value);
        return value;
    }

    protected abstract Task ReceiveSubscribe(Subscriber subscriber, MessageType messageType);
    protected abstract Task ReceiveUnsubscribe(Subscriber subscriber, MessageType messageType);
}