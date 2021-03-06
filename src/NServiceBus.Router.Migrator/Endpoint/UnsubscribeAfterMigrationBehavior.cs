﻿namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    class UnsubscribeAfterMigrationBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        ISubscriptionStorage subscriptionStorage;

        public UnsubscribeAfterMigrationBehavior(ISubscriptionStorage subscriptionStorage)
        {
            this.subscriptionStorage = subscriptionStorage;
        }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            await next();

            if (subscriptionStorage == null)
            {
                //Storage-driven pub/sub is not enabled
                return;
            }

            if (!context.MessageHeaders.TryGetValue("NServiceBus.Router.Migrator.UnsubscribeEndpoint", out var subscriberEndpoint)
                || !context.MessageHeaders.TryGetValue("NServiceBus.Router.Migrator.UnsubscribeType", out var messageTypeString))
            {
                return;
            }

            var messageType = new MessageType(messageTypeString);
            var allSubscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, context.Extensions).ConfigureAwait(false);

            foreach (var subscriber in allSubscribers)
            {
                if (subscriber.Endpoint == subscriberEndpoint)
                {
                    await subscriptionStorage.Unsubscribe(subscriber, messageType, context.Extensions).ConfigureAwait(false);
                }
            }
        }
    }
}