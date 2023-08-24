using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Pipeline;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class RouterAutoSubscribeBehavior : Behavior<IOutgoingPublishContext>
{
    readonly IEnumerable<string> autoPublishRouters;
    readonly ISubscriptionStorage subscriptionStorage;
    readonly ConcurrentDictionary<Type, bool> cache = new ConcurrentDictionary<Type, bool>();

    public RouterAutoSubscribeBehavior(IEnumerable<string> autoPublishRouters, ISubscriptionStorage subscriptionStorage)
    {
        this.autoPublishRouters = autoPublishRouters.ToArray();
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

        foreach (var router in autoPublishRouters)
        {
            var subscriber = new Subscriber(router, null);
            await subscriptionStorage.Subscribe(subscriber, new MessageType(messageType), new ContextBag(), context.CancellationToken).ConfigureAwait(false);
        }

        cache.AddOrUpdate(messageType, true, (key, _) => true);
        await next().ConfigureAwait(false);
    }
}