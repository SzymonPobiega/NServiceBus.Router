using System;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.Unicast.Transport;

static class MessageDrivenPubSub
{
    public static OutgoingMessage CreateMessage(string ultimateDestination, string messageType, string localAddress, string localEndpoint, MessageIntent intent)
    {
        var subscriptionMessage = ControlMessageFactory.Create(intent);

        subscriptionMessage.Headers[Headers.SubscriptionMessageType] = messageType;
        subscriptionMessage.Headers[Headers.ReplyToAddress] = localAddress;
        if (localAddress != null)
        {
            subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = localAddress;
        }
        subscriptionMessage.Headers[Headers.SubscriberEndpoint] = localEndpoint;
        subscriptionMessage.Headers[Headers.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow);
        subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

        if (ultimateDestination != null)
        {
            subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
        }

        return subscriptionMessage;
    }
}