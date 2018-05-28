using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class StorageDrivenSubscriptionReceiver : SubscriptionReceiver
{
    ISubscriptionStorage subscriptionStorage;

    public StorageDrivenSubscriptionReceiver(ISubscriptionStorage subscriptionStorage)
    {
        this.subscriptionStorage = subscriptionStorage;
    }

    protected override Task ReceiveSubscribe(Subscriber subscriber, MessageType messageType)
    {
        return subscriptionStorage.Subscribe(subscriber, messageType, new ContextBag());
    }

    protected override Task ReceiveUnsubscribe(Subscriber subscriber, MessageType messageType)
    {
        return subscriptionStorage.Unsubscribe(subscriber, messageType, new ContextBag());
    }
}