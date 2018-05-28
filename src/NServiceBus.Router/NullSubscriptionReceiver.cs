using System.Threading.Tasks;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class NullSubscriptionReceiver : SubscriptionReceiver
{
    protected override Task ReceiveSubscribe(Subscriber subscriber, MessageType messageType)
    {
        return Task.CompletedTask;
    }

    protected override Task ReceiveUnsubscribe(Subscriber subscriber, MessageType messageType)
    {
        return Task.CompletedTask;
    }
}