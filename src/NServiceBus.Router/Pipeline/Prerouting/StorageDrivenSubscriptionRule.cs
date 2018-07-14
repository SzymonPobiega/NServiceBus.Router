using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class StorageDrivenSubscriptionRule : IRule<SubscribePreroutingContext, SubscribePreroutingContext>
{
    ISubscriptionStorage subscriptionStorage;

    public StorageDrivenSubscriptionRule(ISubscriptionStorage subscriptionStorage)
    {
        this.subscriptionStorage = subscriptionStorage;
    }

    public async Task Invoke(SubscribePreroutingContext context, Func<SubscribePreroutingContext, Task> next)
    {
        var subscriber = new Subscriber(context.SubscriberAddress, context.SubscriberEndpoint);
        var messageType = new MessageType(context.MessageType);

        await subscriptionStorage.Subscribe(subscriber, messageType, context).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}