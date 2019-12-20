namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Transport;
    using Unicast.Transport;

    class ShadowForwardSubscribeRule : ChainTerminator<ForwardSubscribeContext>
    {
        string localAddress;
        string localEndpoint;

        public ShadowForwardSubscribeRule(string localAddress, string localEndpoint)
        {
            this.localAddress = localAddress;
            this.localEndpoint = localEndpoint;
        }

        protected override async Task<bool> Terminate(ForwardSubscribeContext context)
        {
            var immediateSubscribes = context.Routes.Where(r => r.Gateway == null);
            var forkContexts = immediateSubscribes.Select(r =>
                    new MulticastContext(r.Destination,
                        CreateMessage(null, context.MessageType, localAddress, localEndpoint, context.SubscriberAddress, context.SubscriberEndpoint, MessageIntentEnum.Subscribe), context))
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

        static OutgoingMessage CreateMessage(string ultimateDestination, string messageType, string localAddress, string localEndpoint, string originalSubscriberAddress, string originalSubscriberEndpoint, MessageIntentEnum intent)
        {
            var subscriptionMessage = ControlMessageFactory.Create(intent);

            subscriptionMessage.Headers[Headers.SubscriptionMessageType] = messageType;
            subscriptionMessage.Headers[Headers.ReplyToAddress] = localAddress;
            if (localAddress != null)
            {
                subscriptionMessage.Headers[Headers.SubscriberTransportAddress] = localAddress;
            }

            subscriptionMessage.Headers["NServiceBus.Router.Migrator.OriginalSubscriberAddress"] = originalSubscriberAddress;
            subscriptionMessage.Headers["NServiceBus.Router.Migrator.OriginalSubscriberEndpoint"] = originalSubscriberEndpoint;
            subscriptionMessage.Headers[Headers.SubscriberEndpoint] = localEndpoint;
            subscriptionMessage.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
            subscriptionMessage.Headers[Headers.NServiceBusVersion] = "6.3.1"; //The code has been copied from 6.3.1

            if (ultimateDestination != null)
            {
                subscriptionMessage.Headers["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
            }

            return subscriptionMessage;
        }
    }
}