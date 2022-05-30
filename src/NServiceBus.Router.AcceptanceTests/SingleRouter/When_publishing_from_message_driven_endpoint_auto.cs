using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using InMemoryPersistence = global::InMemoryPersistence;

    [TestFixture]
    public class When_publishing_from_message_driven_endpoint_auto : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            var alphaSubscriptionStore = new InMemorySubscriptionStorage();
            var bravoSubscriptionStore = new InMemorySubscriptionStorage();

            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface<TestTransport>("A", t => t.BrokerAlpha()).EnableMessageDrivenPublishSubscribe(alphaSubscriptionStore);
                    cfg.AddInterface<TestTransport>("B", t => t.BrokerBravo()).EnableMessageDrivenPublishSubscribe(bravoSubscriptionStore);
                    cfg.AddInterface<TestTransport>("C", t => t.BrokerYankee());

                    cfg.UseStaticRoutingProtocol();
                })
                .WithEndpoint<Publisher>(c =>
                {
                    c.CustomConfig(cfg =>
                    {
                        cfg.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(alphaSubscriptionStore);
                    }).When(x => x.EndpointsStarted, async (s, ctx) =>
                    {
                        //Need to retry sending because there is no reliable way to figure when the router is subscribed
                        while (!ctx.BaseEventDelivered || !ctx.DerivedEventDelivered) 
                        {
                            await s.Publish(new MyDerivedEvent2());
                            await Task.Delay(1000);
                        }
                    });
                })
                .WithEndpoint<BaseEventSubscriber>(c => c.CustomConfig(cfg =>
                {
                    cfg.UsePersistence<InMemoryPersistence, StorageType.Subscriptions>().UseStorage(bravoSubscriptionStore);
                }))
                .WithEndpoint<DerivedEventSubscriber>()
                .Done(c => c.BaseEventDelivered && c.DerivedEventDelivered)
                .Run();

            Assert.IsTrue(result.BaseEventDelivered);
            Assert.IsTrue(result.DerivedEventDelivered);
        }

        class Context : ScenarioContext
        {
            public bool BaseEventDelivered { get; set; }
            public bool DerivedEventDelivered { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();

                    routing.ConnectToRouter("Router", false, true);
                });
            }
        }

        class BaseEventSubscriber : EndpointConfigurationBuilder
        {
            public BaseEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerBravo()
                        .Routing();

                    routing.ConnectToRouter("Router", true, false);
                });
            }

            class BaseEventHandler : IHandleMessages<MyBaseEvent2>
            {
                Context scenarioContext;

                public BaseEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyBaseEvent2 message, IMessageHandlerContext context)
                {
                    scenarioContext.BaseEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class DerivedEventSubscriber : EndpointConfigurationBuilder
        {
            public DerivedEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerYankee()
                        .Routing();

                    routing.ConnectToRouter("Router", true, false);
                });
            }

            class DerivedEventHandler : IHandleMessages<MyDerivedEvent2>
            {
                Context scenarioContext;

                public DerivedEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyDerivedEvent2 message, IMessageHandlerContext context)
                {
                    scenarioContext.DerivedEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyBaseEvent2 : IEvent
        {
        }

        class MyDerivedEvent2 : MyBaseEvent2
        {
        }
    }
}
