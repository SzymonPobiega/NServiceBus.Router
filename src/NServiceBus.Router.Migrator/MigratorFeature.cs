namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Configuration.AdvancedExtensibility;
    using Extensibility;
    using Features;
    using Pipeline;
    using Routing;
    using Transport;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Unicast.Transport;

    /// <summary>
    /// Configures migrator
    /// </summary>
    public static class MigratorConfigurationExtensions
    {
        internal const string NewTransportCustomizationSettingsKey = "NServiceBus.Router.Migrator.NewTransportCustomization";
        internal const string OldTransportCustomizationSettingsKey = "NServiceBus.Router.Migrator.OldTransportCustomization";

        /// <summary>
        /// Enables transport migration for this endpoint.
        /// </summary>
        /// <typeparam name="TOld">Old transport</typeparam>
        /// <typeparam name="TNew">New transport</typeparam>
        /// <param name="endpointConfiguration">Endpoint configuration</param>
        /// <param name="customizeOldTransport">A callback for customizing the old transport</param>
        /// <param name="customizeNewTransport">A callback for customizing the new transport</param>
        public static void EnableTransportMigration<TOld, TNew>(this EndpointConfiguration endpointConfiguration,
            Action<TransportExtensions<TOld>> customizeOldTransport, Action<TransportExtensions<TNew>> customizeNewTransport)
        where TOld : TransportDefinition, new()
        where TNew : TransportDefinition, new()
        {
            //TODO: Installers

            var endpointTransport = endpointConfiguration.UseTransport<TNew>();
            customizeNewTransport(endpointTransport);

            endpointConfiguration.GetSettings().Set(NewTransportCustomizationSettingsKey, customizeNewTransport);
            endpointConfiguration.GetSettings().Set(OldTransportCustomizationSettingsKey, customizeOldTransport);

            endpointConfiguration.EnableFeature<MigratorFeature<TOld, TNew>>();
        }
    }

    class MigratorFeature<TOld, TNew> : Feature
      where TOld : TransportDefinition, new()
      where TNew : TransportDefinition, new()
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var mainEndpointName = context.Settings.EndpointName();
            var routerEndpointName = $"{mainEndpointName}_Migrator";

            var endpointInstances = context.Settings.Get<EndpointInstances>();
            var distributionPolicy = context.Settings.Get<IDistributionPolicy>();
            var transportInfrastructure = context.Settings.Get<TransportInfrastructure>();

            var customizeOldTransport = context.Settings.Get<Action<TransportExtensions<TOld>>>(MigratorConfigurationExtensions.OldTransportCustomizationSettingsKey);
            var customizeNewTransport = context.Settings.Get<Action<TransportExtensions<TNew>>>(MigratorConfigurationExtensions.NewTransportCustomizationSettingsKey);

            context.Pipeline.Register(new PublishRedirectionBehavior(routerEndpointName, endpointInstances, distributionPolicy, transportInfrastructure),
                "Redirects publishes that target old transport address via the router");

            context.Pipeline.Register(b => new UnsubscribeWhenMigratedBehavior(b.Build<ISubscriptionStorage>()),
                "Removes old transport subscriptions when a new transport subscription for the same event and endpoint comes in");

            var routerConfig = PrepareRouterConfiguration(routerEndpointName, mainEndpointName, context.Settings.LocalAddress(), customizeOldTransport, customizeNewTransport);
            context.RegisterStartupTask(new MigratorStartupTask(routerConfig));
        }

        RouterConfiguration PrepareRouterConfiguration(string routerEndpointName, string mainEndpointName, string mainEndpointAddress, Action<TransportExtensions<TOld>> customizeOldTransport, Action<TransportExtensions<TNew>> customizeNewTransport)
        {
            var cfg = new RouterConfiguration(routerEndpointName);

            var bravoInterface = cfg.AddInterface("New", customizeNewTransport);
            bravoInterface.DisableMessageDrivenPublishSubscribe();

            //Forward unmodified subscribe messages from migrated subscriber
            bravoInterface.AddRule(c => new ForwardSubscribeUnmodifiedRule());

            //Forward published events from shadow interface to migrated subscriber
            bravoInterface.AddRule(c => new ShadowForwardPublishRule(mainEndpointAddress));

            var shadowInterface = cfg.AddInterface("Shadow", customizeOldTransport);
            shadowInterface.DisableMessageDrivenPublishSubscribe();

            //Hook up to old Publisher's queue
            shadowInterface.OverrideEndpointName(mainEndpointName);

            //Forward subscribe messages
            shadowInterface.AddRule(c => new ShadowForwardSubscribeRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));

            //Forward events published by migrated publisher
            shadowInterface.AddRule(c => new ForwardPublishByDestinationAddressRule());

            //Forward subscribes messages from shadow interface to migrated publisher
            shadowInterface.AddRule(c => new ShadowSubscribeDestinationRule(mainEndpointName));

            var staticRouting = cfg.UseStaticRoutingProtocol();
            staticRouting.AddForwardRoute("New", "Shadow");
            staticRouting.AddForwardRoute("Shadow", "New");

            return cfg;
        }

        class MigratorStartupTask : FeatureStartupTask
        {
            IRouter router;

            public MigratorStartupTask(RouterConfiguration routerConfiguration)
            {
                router = Router.Create(routerConfiguration);
            }

            protected override Task OnStart(IMessageSession session)
            {
                return router.Start();
            }

            protected override Task OnStop(IMessageSession session)
            {
                return router.Stop();
            }
        }
    }

    class UnsubscribeWhenMigratedBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        ISubscriptionStorage subscriptionStorage;

        public UnsubscribeWhenMigratedBehavior(ISubscriptionStorage subscriptionStorage)
        {
            this.subscriptionStorage = subscriptionStorage;
        }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            await next();

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

    class PublishRedirectionBehavior : Behavior<IRoutingContext>
    {
        string routerEndpoint;
        EndpointInstances endpointInstances;
        IDistributionPolicy distributionPolicy;
        TransportInfrastructure transportInfrastructure;

        public PublishRedirectionBehavior(string routerEndpoint, EndpointInstances endpointInstances, IDistributionPolicy distributionPolicy, TransportInfrastructure transportInfrastructure)
        {
            this.routerEndpoint = routerEndpoint;
            this.endpointInstances = endpointInstances;
            this.distributionPolicy = distributionPolicy;
            this.transportInfrastructure = transportInfrastructure;
        }

        public override Task Invoke(IRoutingContext context, Func<Task> next)
        {
            var newStrategies = new List<RoutingStrategy>();
            var ignores = context.RoutingStrategies.OfType<UnicastRoutingStrategy>()
                .Select(x => x.Apply(new Dictionary<string, string>()))
                .Cast<UnicastAddressTag>()
                .Select(t => t.Destination)
                .ToArray();

            foreach (var strategy in context.RoutingStrategies)
            {
                if (strategy is UnicastRoutingStrategy unicastStrategy)
                {
                    var routerAddress = CalculateRouterAddress(context.Message.MessageId, context.Message.Headers, context.Extensions.Get<OutgoingLogicalMessage>(), context.Extensions);

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
            return next();
        }

        string CalculateRouterAddress(string messageId, Dictionary<string, string> headers, OutgoingLogicalMessage outgoingLogicalMessage, ContextBag contextBag)
        {
            var distributionStrategy = distributionPolicy.GetDistributionStrategy(routerEndpoint, DistributionStrategyScope.Send);

            string ToTransportAddress(EndpointInstance x) => transportInfrastructure.ToTransportAddress(LogicalAddress.CreateRemoteAddress(x));

            var addresses = endpointInstances.FindInstances(routerEndpoint)
                .Select(ToTransportAddress)
                .ToArray();

            var distributionContext = new DistributionContext(addresses, outgoingLogicalMessage, messageId, headers, ToTransportAddress, contextBag);
            var destination = distributionStrategy.SelectDestination(distributionContext);
            return destination;
        }
    }

    class IgnoreDuplicatesBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        string oldTransportAddress;

        public IgnoreDuplicatesBehavior(string oldTransportAddress)
        {
            this.oldTransportAddress = oldTransportAddress;
        }

        public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            //Do not ignore messages that were delivered via message-driven mechanism
            if (!context.MessageHeaders.TryGetValue("NServiceBus.Router.Migrator.Ignore", out var ignores)
                || context.MessageHeaders.ContainsKey("NServiceBus.Router.Migrator.Forwarded"))
            {
                return next();
            }

            var ignoreAddresses = ignores.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (ignoreAddresses.Contains(oldTransportAddress))
            {
                //This messages has also been sent via message-driven pub sub so we can ignore this one
                return Task.CompletedTask;
            }

            return next();
        }
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

    class ShadowSubscribeDestinationRule : IRule<SubscribePreroutingContext, SubscribePreroutingContext>
    {
        string fixedDestination;

        public ShadowSubscribeDestinationRule(string fixedDestination)
        {
            this.fixedDestination = fixedDestination;
        }

        public Task Invoke(SubscribePreroutingContext context, Func<SubscribePreroutingContext, Task> next)
        {
            context.Destinations.Add(new Destination(fixedDestination, null));
            return next(context);
        }
    }

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

    class ShadowForwardPublishRule : ChainTerminator<ForwardPublishContext>
    {
        string endpointAddress;

        public ShadowForwardPublishRule(string endpointAddress)
        {
            this.endpointAddress = endpointAddress;
        }

        protected override async Task<bool> Terminate(ForwardPublishContext context)
        {
            var op = new TransportOperation(new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody),
                new UnicastAddressTag(endpointAddress));

            var postroutingContext = new PostroutingContext(null, op, context);
            var chain = context.Chains.Get<PostroutingContext>();

            await chain.Invoke(postroutingContext).ConfigureAwait(false);

            return true;
        }
    }

    class ForwardPublishByDestinationAddressRule : ChainTerminator<ForwardPublishContext>
    {
        protected override async Task<bool> Terminate(ForwardPublishContext context)
        {
            if (context.ReceivedHeaders.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var destinationEndpoint))
            {
                context.ForwardedHeaders["NServiceBus.Router.Migrator.Forwarded"] = "true";

                var op = new TransportOperation(new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody),
                    new UnicastAddressTag(destinationEndpoint));

                var postroutingContext = new PostroutingContext(null, op, context);
                var chain = context.Chains.Get<PostroutingContext>();

                await chain.Invoke(postroutingContext).ConfigureAwait(false);

                return true;
            }

            return false;
        }
    }
}
