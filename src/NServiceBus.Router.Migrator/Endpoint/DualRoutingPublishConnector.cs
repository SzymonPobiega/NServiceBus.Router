namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Pipeline;
    using Routing;
    using Unicast.Messages;
    using Unicast.Queuing;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    class DualRoutingPublishConnector : StageConnector<IOutgoingPublishContext, IOutgoingLogicalMessageContext>
    {
        public DualRoutingPublishConnector(string routerAddress, DistributionPolicy distributionPolicy, MessageMetadataRegistry messageMetadataRegistry, Func<EndpointInstance, string> transportAddressTranslation, ISubscriptionStorage subscriptionStorage)
        {
            this.routerAddress = routerAddress;
            this.distributionPolicy = distributionPolicy;
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.transportAddressTranslation = transportAddressTranslation;
            this.subscriptionStorage = subscriptionStorage;
        }

        public override async Task Invoke(IOutgoingPublishContext context, Func<IOutgoingLogicalMessageContext, Task> stage)
        {
            context.Headers[Headers.MessageIntent] = MessageIntentEnum.Publish.ToString();

            var eventType = context.Message.MessageType;
            var unicastRoutingStrategies = await Route(eventType, context).ConfigureAwait(false);

            var dualRoutingEndpoints = unicastRoutingStrategies.Where(r => r.Item2 != null).Select(r => r.Item2).ToArray();
            var multicastRoutingStrategy = new DualRoutingMulticastRoutingStrategy(dualRoutingEndpoints, new MulticastRoutingStrategy(context.Message.MessageType));

            var routingStrategies = new List<RoutingStrategy>
            {
                multicastRoutingStrategy
            };

            routingStrategies.AddRange(unicastRoutingStrategies.Select(x => new MigratorRouterRoutingStrategy(routerAddress, x.Item1)));

            var logicalMessageContext = this.CreateOutgoingLogicalMessageContext(
                context.Message,
                routingStrategies,
                context);

            try
            {
                await stage(logicalMessageContext).ConfigureAwait(false);
            }
            catch (QueueNotFoundException ex)
            {
                throw new Exception($"The destination queue '{ex.Queue}' could not be found. The destination may be misconfigured for this kind of message ({context.Message.MessageType}) in the routing section of the transport configuration. It may also be the case that the given queue hasn\'t been created yet, or has been deleted.", ex);
            }
        }

        async Task<List<(UnicastRoutingStrategy, string)>> Route(Type messageType, IOutgoingPublishContext publishContext)
        {
            var typesToRoute = messageMetadataRegistry.GetMessageMetadata(messageType).MessageHierarchy;

            var subscribers = await GetSubscribers(publishContext, typesToRoute).ConfigureAwait(false);

            var destinations = SelectDestinationsForEachEndpoint(publishContext, distributionPolicy, subscribers);

            WarnIfNoSubscribersFound(messageType, destinations.Count);

            return destinations;
        }

        void WarnIfNoSubscribersFound(Type messageType, int subscribersFound)
        {
            if (subscribersFound == 0)
            {
                logger.DebugFormat("No subscribers found for the event of type {0}.", messageType.FullName);
            }
        }

        List<(UnicastRoutingStrategy, string)> SelectDestinationsForEachEndpoint(IOutgoingPublishContext publishContext, IDistributionPolicy distributionPolicy, IEnumerable<Subscriber> subscribers)
        {
            //Make sure we are sending only one to each transport destination. Might happen when there are multiple routing information sources.
            var addresses = new Dictionary<string, UnicastRoutingStrategy>();
            Dictionary<string, List<string>> groups = null;
            var results = new List<(UnicastRoutingStrategy, string)>();
            foreach (var subscriber in subscribers)
            {
                if (subscriber.Endpoint == null)
                {
                    if (!addresses.ContainsKey(subscriber.TransportAddress))
                    {
                        var strategy = new UnicastRoutingStrategy(subscriber.TransportAddress);
                        addresses.Add(subscriber.TransportAddress, strategy);
                        results.Add((strategy, null));
                        logger.Warn($"Subscription {subscriber.TransportAddress} comes from NServiceBus endpoint older than V6. That endpoint needs to be upgraded to at least V6 and restarted in order to be migrated to the new transport.");

                    }

                    continue;
                }

                groups = groups ?? new Dictionary<string, List<string>>();

                if (groups.TryGetValue(subscriber.Endpoint, out var transportAddresses))
                {
                    transportAddresses.Add(subscriber.TransportAddress);
                }
                else
                {
                    groups[subscriber.Endpoint] = new List<string> { subscriber.TransportAddress };
                }
            }

            if (groups != null)
            {
                foreach (var group in groups)
                {
                    var instances = group.Value.ToArray(); // could we avoid this?
                    var distributionContext = new DistributionContext(instances, publishContext.Message, publishContext.MessageId, publishContext.Headers, transportAddressTranslation, publishContext.Extensions);
                    var subscriber = distributionPolicy.GetDistributionStrategy(group.Key, DistributionStrategyScope.Publish).SelectDestination(distributionContext);

                    if (!addresses.ContainsKey(subscriber))
                    {
                        var strategy = new UnicastRoutingStrategy(subscriber);
                        addresses.Add(subscriber, strategy);
                        results.Add((strategy, group.Key));
                    }
                }
            }

            return results;
        }

        Task<IEnumerable<Subscriber>> GetSubscribers(IExtendable publishContext, Type[] typesToRoute)
        {
            var messageTypes = typesToRoute.Select(t => new MessageType(t));
            return subscriptionStorage.GetSubscriberAddressesForMessage(messageTypes, publishContext.Extensions);
        }

        MessageMetadataRegistry messageMetadataRegistry;
        Func<EndpointInstance, string> transportAddressTranslation;
        ISubscriptionStorage subscriptionStorage;
        string routerAddress;
        DistributionPolicy distributionPolicy;

        static ILog logger = LogManager.GetLogger<DualRoutingPublishConnector>();

    }
}