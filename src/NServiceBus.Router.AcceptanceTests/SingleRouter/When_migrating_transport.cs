namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using Features;
    using Migrator;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;
    using Routing;
    using Transport;
    using Unicast.Messages;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;
    using Unicast.Transport;
    using InMemoryPersistence = global::InMemoryPersistence;

    [TestFixture]
    public class When_migrating_transport : NServiceBusAcceptanceTest
    {
        const string SubRouterAddress = "SubRouter";
        const string PubRouterAddress = "PubRouter";
        static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));
        static string SubscriberEndpointName => Conventions.EndpointNamingConvention(typeof(Subscriber));

        [Test]
        public async Task Should_not_lose_events_if_subscriber_is_migrated_first()
        {
            var subscriptionStorage = new InMemorySubscriptionStorage();

            var beforeMigration = await Scenario.Define<Context>(c => c.Step = "Before migration")
                .WithEndpoint<Publisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Subscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Subscribe(typeof(MyEvent))))
                .Done(c => c.EventReceivedByNonMigratedSubscriber)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(beforeMigration.EventReceivedByNonMigratedSubscriber);

            //Migrate subscriber
            //After the subscriber is migrated we should not need to re-subscribe to be able to receive events
            //To prove that we don't call subscribe in this test run
            var subscriberMigrated = await Scenario.Define<Context>(c => c.Step = "Subscriber migrated")
                .WithRouter(SubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, SubscriberEndpointName))
                .WithEndpoint<Publisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.EndpointsStarted, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>()
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(subscriberMigrated.EventsReceivedByMigratedSubscriber >= 1);

            //After restarting the subscriber it sends the subscribe message to the publisher
            //To prove that a subscribe message can reach to publisher via the router we use a brand new subscription store
            subscriptionStorage = new InMemorySubscriptionStorage();
            var subscriberResubscribed = await Scenario.Define<Context>(c => c.Step = "Resubscribed after migration")
                .WithRouter(SubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, SubscriberEndpointName))
                .WithEndpoint<Publisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Subscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Subscribe(typeof(MyEvent))))
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(subscriberResubscribed.EventsReceivedByMigratedSubscriber >= 1);

            //After migrating but prior to resubscribing we should detect that the published messages gets through two routers and drop it
            var publisherMigrated = await Scenario.Define<Context>(c => c.Step = "Publisher migrated")
                .WithRouter(SubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, SubscriberEndpointName))
                .WithRouter(PubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, PublisherEndpointName))
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.EndpointsStarted, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>()
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1 && c.DuplicatePublishDetected)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(publisherMigrated.EventsReceivedByMigratedSubscriber >= 1);

            //After resubscribing we should detect that the subscribe messages gets through two routers and drop the old subscription
            var resubscribedAfterPublisherMigrated = await Scenario.Define<Context>(c => c.Step = "Resubscribed after publisher migrated")
                .WithRouter(SubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, SubscriberEndpointName))
                .WithRouter(PubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, PublisherEndpointName))
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Unsubscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Subscribe(typeof(MyEvent))))
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1 && c.Unsubscribed)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(resubscribedAfterPublisherMigrated.EventsReceivedByMigratedSubscriber >= 1);

            //Compatibility mode disabled after all endpoints are migrated
            var compatModeDisabled = await Scenario.Define<Context>(c => c.Step = "Compatibility mode disabled")
                .WithEndpoint<MigratedPublisherNoCompatMode>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriberNoCompatMode>()
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(compatModeDisabled.EventsReceivedByMigratedSubscriber >= 1);
        }

        [Test]
        public async Task Should_not_lose_events_if_publisher_is_migrated_first()
        {
            var subscriptionStorage = new InMemorySubscriptionStorage();

            var beforeMigration = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Subscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Subscribe(typeof(MyEvent))))
                .Done(c => c.EventReceivedByNonMigratedSubscriber)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(beforeMigration.EventReceivedByNonMigratedSubscriber);

            //After the publisher is migrated we should not need to re-subscribe to be able to receive events
            //To prove that we don't call subscribe in this test run
            var publisherMigrated = await Scenario.Define<Context>()
                .WithRouter(PubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, PublisherEndpointName))
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.EndpointsStarted, s => s.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>()
                .Done(c => c.EventReceivedByNonMigratedSubscriber)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(publisherMigrated.EventReceivedByNonMigratedSubscriber);

            //After restarting the subscriber it sends the subscribe message to a migrated publisher
            //To prove that a subscribe message can reach to publisher via the router we use a brand new subscription store

            subscriptionStorage = new InMemorySubscriptionStorage();
            var subscriberResubscribed = await Scenario.Define<Context>()
                .WithRouter(PubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, PublisherEndpointName))
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Subscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Subscribe(typeof(MyEvent))))
                .Done(c => c.EventReceivedByNonMigratedSubscriber)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(subscriberResubscribed.EventReceivedByNonMigratedSubscriber);

            //Migrate subscriber
            var subscriberMigrated = await Scenario.Define<Context>()
                .WithRouter(PubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, PublisherEndpointName))
                .WithRouter(SubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, SubscriberEndpointName))
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.EndpointsStarted, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>()
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.AreEqual(1, subscriberMigrated.EventsReceivedByMigratedSubscriber);

            //Re-subscribe natively after migration. Use MigratedSubscriberNoCompatMode to only subscribe natively
            var resubscribedNativeAfterMigration = await Scenario.Define<Context>(c => c.Step = "Resubscribed after migrated")
                .WithRouter(PubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, PublisherEndpointName))
                .WithRouter(SubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, SubscriberEndpointName))
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Subscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriberNoCompatMode>(c => c.When(ctx => ctx.EndpointsStarted, async (s, ctx) =>
                {
                    await s.Subscribe(typeof(MyEvent));
                    ctx.Subscribed = true;
                }))
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1 && c.DuplicatePublishDetected)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(resubscribedNativeAfterMigration.DuplicatePublishDetected);
            Assert.AreEqual(1, resubscribedNativeAfterMigration.EventsReceivedByMigratedSubscriber);

            //Re-subscribe after migration. This will send a subscribe message that will trigger removal of old subscription -- Unsubscribed will be set
            var resubscribedAfterMigration = await Scenario.Define<Context>(c => c.Step = "Resubscribed after migrated")
                .WithRouter(PubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, PublisherEndpointName))
                .WithRouter(SubRouterAddress, (ctx, cfg) => ConfigureRouter(ctx, cfg, SubscriberEndpointName))
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Unsubscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>(c => c.When(ctx => ctx.EndpointsStarted, (s, ctx) => s.Subscribe(typeof(MyEvent))))
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.AreEqual(1, resubscribedAfterMigration.EventsReceivedByMigratedSubscriber);

            //Compatibility mode disabled after all endpoints are migrated
            var compatModeDisabled = await Scenario.Define<Context>(c => c.Step = "Compatibility mode disabled")
                .WithEndpoint<MigratedPublisherNoCompatMode>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriberNoCompatMode>()
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(compatModeDisabled.EventsReceivedByMigratedSubscriber >= 1);
        }

        void ConfigureRouter(Context scenarioContext, RouterConfiguration cfg, string shadowEndpointName)
        {
            //When migrated subscriber subscribes, send a subscription message via router to the destination. Subscribe as shadow iface
            //When subscription message goes through two interfaces, make it unsubscribe to clean up subscription store
            //When migrated endpoint publishes, forward unicast operations via router as the subscription store contains old addresses
            //When non-migrated endpoint subscribes, forward that subscribe from shadow iface unmodified (preserve old transport address)
            //When non-migrated endpoint publishes, forward published message from shadow iface

            var newInterface = cfg.AddInterface<TestTransport>("New", t => t.BrokerYankee());
            newInterface.DisableNativePubSub();

            //Forward unmodified subscribe messages from migrated subscriber
            newInterface.AddRule(c => new ForwardSubscribeUnmodifiedRule());

            //Forward published events from shadow interface to migrated subscriber
            newInterface.AddRule(c => new ShadowForwardPublishRule(shadowEndpointName));
            //newInterface.AddRule(c => new ShadowForwardPublishRuleDetector(scenarioContext));

            var shadowInterface = cfg.AddInterface<TestTransport>("Old", t => t.BrokerAlpha());
            shadowInterface.DisableMessageDrivenPublishSubscribe();

            //Hook up to old Publisher's queue
            shadowInterface.OverrideEndpointName(shadowEndpointName);

            //Forward subscribe messages
            shadowInterface.AddRule(c => new ShadowForwardSubscribeRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));

            //Forward events published by migrated publisher
            shadowInterface.AddRule(c => new ForwardPublishByDestinationAddressRule());

            //Forward subscribes messages from shadow interface to migrated publisher
            shadowInterface.AddRule(c => new ShadowSubscribeDestinationRule(shadowEndpointName));

            var staticRouting = cfg.UseStaticRoutingProtocol();
            staticRouting.AddForwardRoute("New", "Old");
            staticRouting.AddForwardRoute("Old", "New");
        }

        class Context : ScenarioContext
        {
            public bool Subscribed { get; set; }
            public bool EventReceivedByNonMigratedSubscriber { get; set; }
            public int EventsReceivedByMigratedSubscriber { get; set; }
            public string Step { get; set; }
            public bool Unsubscribed { get; set; }
            public bool DuplicatePublishDetected { get; set; }
        }

        class UnsubscribeWhenMigratedDetector : Behavior<IIncomingPhysicalMessageContext>
        {
            ISubscriptionStorage subscriptionStorage;
            Context scenarioContext;

            public UnsubscribeWhenMigratedDetector(ISubscriptionStorage subscriptionStorage, Context scenarioContext)
            {
                this.subscriptionStorage = subscriptionStorage;
                this.scenarioContext = scenarioContext;
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
                        scenarioContext.Unsubscribed = true;
                    }
                }
            }
        }

        class PublishRedirectionBehavior : Behavior<IRoutingContext>
        {
            string routerAddress;
            ISubscriptionStorage subscriptionStorage;
            MessageMetadataRegistry metadataRegistry;

            public PublishRedirectionBehavior(string routerAddress, ISubscriptionStorage subscriptionStorage, MessageMetadataRegistry metadataRegistry)
            {
                this.routerAddress = routerAddress;
                this.subscriptionStorage = subscriptionStorage;
                this.metadataRegistry = metadataRegistry;
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
        }

        class IgnoreDuplicatesDetector : Behavior<IIncomingPhysicalMessageContext>
        {
            string oldTransportAddress;
            Context scenarioContext;

            public IgnoreDuplicatesDetector(string oldTransportAddress, Context scenarioContext)
            {
                this.oldTransportAddress = oldTransportAddress;
                this.scenarioContext = scenarioContext;
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
                    scenarioContext.DuplicatePublishDetected = true;
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

        //class ShadowForwardPublishRuleDetector : ChainTerminator<ForwardPublishContext>
        //{
        //    Context scenarioContext;

        //    public ShadowForwardPublishRuleDetector(Context scenarioContext)
        //    {
        //        this.scenarioContext = scenarioContext;
        //    }

        //    protected override Task<bool> Terminate(ForwardPublishContext context)
        //    {
        //        if (context.ForwardedHeaders.ContainsKey("NServiceBus.Router.Migrator.Forwarded"))
        //        {
        //            scenarioContext.DuplicatePublishDropped = true;
        //        }

        //        return Task.FromResult(false);
        //    }
        //}

        class ShadowForwardPublishRule : ChainTerminator<ForwardPublishContext>
        {
            string fixedDestination;

            public ShadowForwardPublishRule(string fixedDestination)
            {
                this.fixedDestination = fixedDestination;
            }

            protected override async Task<bool> Terminate(ForwardPublishContext context)
            {
                //if (context.ForwardedHeaders.ContainsKey("NServiceBus.Router.Migrator.Forwarded"))
                //{
                //    //Do not forward a message back
                //    context.DoNotRequireThisMessageToBeForwarded();
                //    return true;
                //}

                var op = new TransportOperation(new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody),
                    new UnicastAddressTag(fixedDestination));

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

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerAlpha();
                    c.OnEndpointSubscribed<Context>((args, ctx) =>
                    {
                        ctx.Subscribed = true;
                    });
                });
            }
        }

        class MigratedPublisher : EndpointConfigurationBuilder
        {
            public MigratedPublisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerYankee();
                    c.OnEndpointSubscribed<Context>((args, ctx) =>
                    {
                        ctx.Subscribed = true;
                    });
                    c.GetSettings().Set("NServiceBus.Subscriptions.EnableMigrationMode", true);
                    //Treat all addresses as old
                    c.Pipeline.Register(new PublishRedirectionBehavior(PubRouterAddress), "Redirects events to the router.");
                    c.Pipeline.Register(b => new UnsubscribeWhenMigratedDetector(b.Build<ISubscriptionStorage>(), b.Build<Context>()), "Removes old subscriptions");
                }).CustomEndpointName(PublisherEndpointName);
            }
        }

        class MigratedPublisherNoCompatMode : EndpointConfigurationBuilder
        {
            public MigratedPublisherNoCompatMode()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerYankee();
                }).CustomEndpointName(PublisherEndpointName);
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    routing.RegisterPublisher(typeof(MyEvent), PublisherEndpointName);
                    c.DisableFeature<AutoSubscribe>();
                });
            }

            class MyEventHandler : IHandleMessages<MyEvent>
            {
                Context scenarioContext;

                public MyEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.EventReceivedByNonMigratedSubscriber = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MigratedSubscriber : EndpointConfigurationBuilder
        {
            public MigratedSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    
                    c.DisableFeature<AutoSubscribe>();

                    c.EnableTransportMigration<TestTransport, TestTransport>(to =>
                    {
                        c.UseTransport<TestTransport>().BrokerAlpha();
                    }, tn =>
                    {
                        var routing = c.UseTransport<TestTransport>().BrokerYankee().Routing();
                        var router = routing.ConnectToRouter(SubRouterAddress);
                        router.RegisterPublisher(typeof(MyEvent), PublisherEndpointName);
                    });

                    c.GetSettings().Set("NServiceBus.Subscriptions.EnableMigrationMode", true);
                    c.Pipeline.Register(b => new IgnoreDuplicatesDetector($"{SubscriberEndpointName}@Alpha", b.Build<Context>()), 
                        "Ignores events published both natively and via message driven pub sub");
                }).CustomEndpointName(SubscriberEndpointName);
            }

            class MyEventHandler : IHandleMessages<MyEvent>
            {
                Context scenarioContext;

                public MyEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.EventsReceivedByMigratedSubscriber++;
                    return Task.CompletedTask;
                }
            }
        }

        class MigratedSubscriberNoCompatMode : EndpointConfigurationBuilder
        {
            public MigratedSubscriberNoCompatMode()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerYankee();
                    c.DisableFeature<AutoSubscribe>();
                    c.Pipeline.Register(b => new IgnoreDuplicatesDetector($"{SubscriberEndpointName}@Alpha", b.Build<Context>()),
                        "Ignores events published both natively and via message driven pub sub");
                }).CustomEndpointName(SubscriberEndpointName);
            }

            class MyEventHandler : IHandleMessages<MyEvent>
            {
                Context scenarioContext;

                public MyEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.EventsReceivedByMigratedSubscriber++;
                    return Task.CompletedTask;
                }
            }
        }

        class MyEvent : IEvent
        {
        }
    }
}
