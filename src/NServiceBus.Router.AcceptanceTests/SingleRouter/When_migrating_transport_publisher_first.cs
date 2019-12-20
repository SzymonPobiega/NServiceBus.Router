namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Features;
    using Migrator;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;
    using InMemoryPersistence = global::InMemoryPersistence;

    [TestFixture]
    public class When_migrating_transport_publisher_first : NServiceBusAcceptanceTest
    {
        static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));
        static string SubscriberEndpointName => Conventions.EndpointNamingConvention(typeof(Subscriber));
        static string SubRouterAddress => SubscriberEndpointName + "_Migrator";

        [Test]
        public async Task Should_not_lose_events()
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

            //After the publisher is migrated we should not need to re-subscribe to be able to receive events
            //To prove that we don't call subscribe in this test run
            var publisherMigrated = await Scenario.Define<Context>(c => c.Step = "Publisher migrated")
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
            var subscriberResubscribed = await Scenario.Define<Context>(c => c.Step = "Resubscribed after publisher migrated")
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.Subscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Subscribe(typeof(MyEvent))))
                .Done(c => c.EventReceivedByNonMigratedSubscriber)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(subscriberResubscribed.EventReceivedByNonMigratedSubscriber);

            //Migrate subscriber
            var subscriberMigrated = await Scenario.Define<Context>(c => c.Step = "Subscriber migrated")
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                }).When(ctx => ctx.EndpointsStarted, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>()
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.AreEqual(1, subscriberMigrated.EventsReceivedByMigratedSubscriber);

            var resubscribedNativeAfterMigration = await Scenario.Define<Context>(c => c.Step = "Resubscribed native after subscriber migrated")
                .WithEndpoint<MigratedPublisher>(c => c.CustomConfig(config =>
                {
                    config.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                    config.Pipeline.Register(new UnsubscribeWhenMigratedSuppressingBehavior(), "Disables the unsubscribe behavior");

                }).When(ctx => ctx.Subscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<MigratedSubscriber>(c => c.When(ctx => ctx.EndpointsStarted, async (s, ctx) =>
                {
                    await s.Subscribe(typeof(MyEvent));
                    ctx.Subscribed = true;
                }))
                .Done(c => c.EventsReceivedByMigratedSubscriber >= 1 && c.EventsReceivedAtTransportLevel >= 2)
                .Run(TimeSpan.FromSeconds(30));

            Assert.AreEqual(2, resubscribedNativeAfterMigration.EventsReceivedAtTransportLevel);
            Assert.AreEqual(1, resubscribedNativeAfterMigration.EventsReceivedByMigratedSubscriber);

            //Re-subscribe after migration. This will send a subscribe message that will trigger removal of old subscription -- Unsubscribed will be set
            var resubscribedAfterMigration = await Scenario.Define<Context>(c => c.Step = "Resubscribed after subscriber migrated")
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

        class Context : ScenarioContext
        {
            public bool Subscribed { get; set; }
            public bool EventReceivedByNonMigratedSubscriber { get; set; }
            public int EventsReceivedByMigratedSubscriber { get; set; }
            public string Step { get; set; }
            public bool Unsubscribed { get; set; }
            public int EventsReceivedAtTransportLevel { get; set; }
        }

        class UnsubscribeWhenMigratedDetector : Behavior<ITransportReceiveContext>
        {
            ISubscriptionStorage subscriptionStorage;
            Context scenarioContext;

            public UnsubscribeWhenMigratedDetector(ISubscriptionStorage subscriptionStorage, Context scenarioContext)
            {
                this.subscriptionStorage = subscriptionStorage;
                this.scenarioContext = scenarioContext;
            }

            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (!context.Message.Headers.TryGetValue("NServiceBus.Router.Migrator.UnsubscribeEndpoint", out var subscriberEndpoint)
                    || !context.Message.Headers.TryGetValue("NServiceBus.Router.Migrator.UnsubscribeType", out var messageTypeString))
                {
                    await next();
                    return;
                }

                var messageType = new MessageType(messageTypeString);
                var allSubscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, context.Extensions).ConfigureAwait(false);
                var wasSubscribed = allSubscribers.Any(s => s.Endpoint == subscriberEndpoint);

                await next();

                allSubscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, context.Extensions).ConfigureAwait(false);
                var isSubscribed = allSubscribers.Any(s => s.Endpoint == subscriberEndpoint);

                scenarioContext.Unsubscribed = wasSubscribed && !isSubscribed;
            }
        }

        /// <summary>
        /// Ensures that unsubscribe message is ignored in order to check if de-duplication works
        /// </summary>
        class UnsubscribeWhenMigratedSuppressingBehavior : Behavior<ITransportReceiveContext>
        {
            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                if (context.Message.Headers.TryGetValue("NServiceBus.Router.Migrator.UnsubscribeEndpoint", out _)
                    || context.Message.Headers.TryGetValue("NServiceBus.Router.Migrator.UnsubscribeType", out _))
                {
                    return;
                }

                await next();
            }
        }

        class IgnoreDuplicatesDetector : Behavior<ITransportReceiveContext>
        {
            Context scenarioContext;

            public IgnoreDuplicatesDetector(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                await next().ConfigureAwait(false);
                scenarioContext.EventsReceivedAtTransportLevel++;
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
                    c.OnEndpointSubscribed<Context>((args, ctx) =>
                    {
                        ctx.Subscribed = true;
                    });
                    c.Pipeline.Register(b => new UnsubscribeWhenMigratedDetector(b.Build<ISubscriptionStorage>(), b.Build<Context>()), "Removes old subscriptions");
                    c.EnableTransportMigration<TestTransport, TestTransport>(oldTrans =>
                    {
                        oldTrans.BrokerAlpha();
                    }, newTrans =>
                    {
                        newTrans.BrokerYankee();
                    });
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
                    c.DisableFeature<AutoSubscribe>();
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    routing.RegisterPublisher(typeof(MyEvent), PublisherEndpointName);
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
                    var routing = c.EnableTransportMigration<TestTransport, TestTransport>(to =>
                    {
                        to.BrokerAlpha();
                    }, tn =>
                    {
                        tn.BrokerYankee();
                    });
                    routing.RegisterPublisher(typeof(MyEvent), PublisherEndpointName);

                    c.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(new InMemorySubscriptionStorage());
                    c.Pipeline.Register(b => new IgnoreDuplicatesDetector(b.Build<Context>()),
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
                    c.DisableFeature<AutoSubscribe>();
                    c.UseTransport<TestTransport>().BrokerYankee();

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
