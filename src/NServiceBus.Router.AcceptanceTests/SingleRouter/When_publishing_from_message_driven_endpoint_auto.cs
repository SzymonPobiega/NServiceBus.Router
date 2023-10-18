using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
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
                    var a = cfg.AddInterface("A", false);
                    a.EnableMessageDrivenPublishSubscribe(alphaSubscriptionStore);
                    a.Broker().Alpha();

                    var b = cfg.AddInterface("B", false);
                    b.EnableMessageDrivenPublishSubscribe(bravoSubscriptionStore);
                    b.Broker().Bravo();

                    cfg.AddInterface("C").Broker().Yankee();

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
                    c.ConfigureBroker().Alpha();
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    var routing = c.ConfigureRouting();

                    routing.ConnectToRouter("Router", false, true);
                }).IncludeType<InMemorySubscriptionPersistence>();
            }
        }

        class BaseEventSubscriber : EndpointConfigurationBuilder
        {
            public BaseEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Bravo();
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    var routing = c.ConfigureRouting();
                    
                    routing.ConnectToRouter("Router", true, false);
                }).IncludeType<InMemorySubscriptionPersistence>();
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
                    c.ConfigureBroker().Yankee();

                    var routing = c.ConfigureRouting();

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
