using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_publishing_from_message_driven_pubsub_endpoint : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface("Left", false).Broker().Alpha();
                    cfg.AddInterface("Right", false).Broker().Bravo();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent())))
                .WithEndpoint<BaseEventSubscriber>()
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
            public bool BaseEventSubscribed { get; set; }
            public bool DerivedEventSubscribed { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.ConfigureBroker().Alpha();
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();


                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        if (args.MessageType.Contains("MyBaseEvent"))
                        {
                            context.BaseEventSubscribed = true;
                        }
                        else
                        {
                            context.DerivedEventSubscribed = true;
                        }
                    });
                });
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
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyBaseEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
                });
            }

            class BaseEventHandler : IHandleMessages<MyBaseEvent>
            {
                Context scenarioContext;

                public BaseEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyBaseEvent message, IMessageHandlerContext context)
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
                    c.ConfigureBroker().Bravo();

                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyDerivedEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
                });
            }

            class DerivedEventHandler : IHandleMessages<MyDerivedEvent>
            {
                Context scenarioContext;

                public DerivedEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyDerivedEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.DerivedEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyBaseEvent : IEvent
        {
        }

        class MyDerivedEvent : MyBaseEvent
        {
        }
    }
}
