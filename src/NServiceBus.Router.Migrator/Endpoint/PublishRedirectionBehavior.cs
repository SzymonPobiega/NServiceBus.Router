namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Pipeline;
    using Routing;
    using Unicast.Messages;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    class PublishRedirectionBehavior : Behavior<IRoutingContext>
    {
        string routerEndpoint;
        ISubscriptionStorage subscriptionStorage;
        MessageMetadataRegistry metadataRegistry;
        EndpointInstances endpointInstances;
        IDistributionPolicy distributionPolicy;
        Func<EndpointInstance, string> toTransportAddress;

        public PublishRedirectionBehavior(string routerEndpoint, ISubscriptionStorage subscriptionStorage, MessageMetadataRegistry metadataRegistry, EndpointInstances endpointInstances, IDistributionPolicy distributionPolicy, Func<EndpointInstance, string> toTransportAddress)
        {
            this.routerEndpoint = routerEndpoint;
            this.subscriptionStorage = subscriptionStorage;
            this.metadataRegistry = metadataRegistry;
            this.endpointInstances = endpointInstances;
            this.distributionPolicy = distributionPolicy;
            this.toTransportAddress = toTransportAddress;
        }

        public override async Task Invoke(IRoutingContext context, Func<Task> next)
        {
            var messageType = context.Extensions.Get<OutgoingLogicalMessage>().MessageType;
            var allMessageTypes = metadataRegistry.GetMessageMetadata(messageType).MessageHierarchy;
            var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(allMessageTypes.Select(t => new MessageType(t)), context.Extensions).ConfigureAwait(false);

            var newStrategies = new List<RoutingStrategy>();
            var unicastDestinations = context.RoutingStrategies.OfType<UnicastRoutingStrategy>()
                .Select(x => x.Apply(new Dictionary<string, string>()))
                .Cast<UnicastAddressTag>()
                .Select(t => t.Destination)
                .ToArray();

            //We tell endpoints to which we send unicast messages to ignore the multicast message
            var ignores = subscribers.Where(s => s.Endpoint != null && unicastDestinations.Contains(s.TransportAddress))
                .Select(s => s.Endpoint)
                .ToArray();

            var routerAddress = SelectRouterAddress(context);

            foreach (var strategy in context.RoutingStrategies)
            {
                if (strategy is UnicastRoutingStrategy unicastStrategy)
                {
                    var redirectStrategy = new RedirectRoutingStrategy(routerAddress, unicastStrategy);
                    newStrategies.Add(redirectStrategy);
                }
                else if (strategy is MulticastRoutingStrategy multicastStrategy)
                {
                    var ignoreStrategy = new IgnoreMulticastRoutingStrategy(ignores, multicastStrategy);
                    newStrategies.Add(ignoreStrategy);
                }
                else
                {
                    newStrategies.Add(strategy);
                }
            }

            context.RoutingStrategies = newStrategies;
            await next().ConfigureAwait(false);
        }

        string SelectRouterAddress(IRoutingContext context)
        {
            var routerEndpointInstances = endpointInstances.FindInstances(routerEndpoint);
            var routerAddresses = routerEndpointInstances.Select(toTransportAddress).ToArray();
            var distStrategy = distributionPolicy.GetDistributionStrategy(routerEndpoint, DistributionStrategyScope.Send);
            var distContext = new DistributionContext(routerAddresses, context.Extensions.Get<OutgoingLogicalMessage>(), context.Message.MessageId, context.Message.Headers, toTransportAddress, context.Extensions);
            var routerAddress = distStrategy.SelectDestination(distContext);
            return routerAddress;
        }

        class IgnoreMulticastRoutingStrategy : RoutingStrategy
        {
            MulticastRoutingStrategy multicastStrategy;
            string[] ignores;

            public IgnoreMulticastRoutingStrategy(string[] ignores, MulticastRoutingStrategy multicastStrategy)
            {
                this.ignores = ignores;
                this.multicastStrategy = multicastStrategy;
            }

            public override AddressTag Apply(Dictionary<string, string> headers)
            {
                //Todo replace with multi header
                headers.Add("NServiceBus.Router.Migrator.Ignore", string.Join("|", ignores));
                return multicastStrategy.Apply(headers);
            }
        }

        class RedirectRoutingStrategy : RoutingStrategy
        {
            string routerAddress;
            UnicastRoutingStrategy unicastStrategy;

            public RedirectRoutingStrategy(string routerAddress, UnicastRoutingStrategy unicastStrategy)
            {
                this.routerAddress = routerAddress;
                this.unicastStrategy = unicastStrategy;
            }

            public override AddressTag Apply(Dictionary<string, string> headers)
            {
                var unicastTag = (UnicastAddressTag)unicastStrategy.Apply(headers);
                headers["NServiceBus.Bridge.DestinationEndpoint"] = unicastTag.Destination;
                return new UnicastAddressTag(routerAddress);
            }
        }
    }
}