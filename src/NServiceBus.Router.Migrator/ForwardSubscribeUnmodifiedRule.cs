namespace NServiceBus.Router.Migrator
{
    using System.Linq;
    using System.Threading.Tasks;
    using Transport;

    class ForwardSubscribeUnmodifiedRule : ChainTerminator<ForwardSubscribeContext>
    {
        protected override async Task<bool> Terminate(ForwardSubscribeContext context)
        {
            //If this message came from a another migrator router it means both subscriber and publisher are migrated
            //We can unsubscribe as both are not using native pub sub
            if (context.ForwardedHeaders.TryGetValue("NServiceBus.Router.Migrator.OriginalSubscriberEndpoint", out var originalSubscriberEndpoint)
                && context.ForwardedHeaders.TryGetValue(Headers.SubscriptionMessageType, out var eventType))
            {
                context.ForwardedHeaders.Remove(Headers.SubscriberEndpoint);
                context.ForwardedHeaders.Remove(Headers.SubscriptionMessageType);
                context.ForwardedHeaders.Remove(Headers.ReplyToAddress);
                context.ForwardedHeaders.Remove(Headers.MessageIntent);
                context.ForwardedHeaders["NServiceBus.Router.Migrator.UnsubscribeEndpoint"] = originalSubscriberEndpoint;
                context.ForwardedHeaders["NServiceBus.Router.Migrator.UnsubscribeType"] = eventType;
            }

            var immediateSubscribes = context.Routes.Where(r => r.Gateway == null);
            var forkContexts = immediateSubscribes.Select(r =>
                    new MulticastContext(r.Destination, new OutgoingMessage(context.MessageId, context.ForwardedHeaders, new byte[0]), context))
                .ToArray();

            if (!forkContexts.Any())
            {
                return false;
            }
            var chain = context.Chains.Get<MulticastContext>();
            var forkTasks = forkContexts.Select(c => chain.Invoke(c));
            await Task.WhenAll(forkTasks).ConfigureAwait(false);

            return true;
        }
    }
}