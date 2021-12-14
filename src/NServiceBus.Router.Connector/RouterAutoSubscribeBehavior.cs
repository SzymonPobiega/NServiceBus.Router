using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Pipeline;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class RouterAutoSubscribeBehavior : Behavior<IOutgoingPublishContext>
{
    string[] autoSubscribedRouters;
    readonly ISubscriptionStorage subscriptionStorage;
    readonly ConcurrentDictionary<Type, bool> cache = new ConcurrentDictionary<Type, bool>();

    public RouterAutoSubscribeBehavior(string[] autoSubscribedRouters, ISubscriptionStorage subscriptionStorage)
    {
        this.autoSubscribedRouters = autoSubscribedRouters;
        this.subscriptionStorage = subscriptionStorage;
    }

    public override async Task Invoke(IOutgoingPublishContext context, Func<Task> next)
    {
        var messageType = context.Message.MessageType;

        //Caches the subscription information at runtime to prevent subscribing on each publish call
        if (cache.ContainsKey(messageType))
        {
            await next().ConfigureAwait(false);
            return;
        }

        foreach (var router in autoSubscribedRouters)
        {
            var subscriber = new Subscriber(router, null);
            await subscriptionStorage.Subscribe(subscriber, new MessageType(messageType), new ContextBag()).ConfigureAwait(false);
        }

        cache.AddOrUpdate(messageType, true, (key, _) => true);
        await next().ConfigureAwait(false);
    }
}