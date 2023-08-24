using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Features;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_publishing_from_native_pubsub_endpoint : NServiceBusAcceptanceTest
    {
        static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    var left = cfg.AddInterface("Left");
                    left.Broker().Yankee();
                    //To ensure when tracer arrives the subscribe request has already been processed.
                    left.LimitMessageProcessingConcurrencyTo(1);
                    
                    cfg.AddInterface("Right", false).Broker().Alpha();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent1())))
                .WithEndpoint<BaseEventSubscriber>(c => c.When(async s =>
                {
                    await s.Subscribe<MyBaseEvent1>().ConfigureAwait(false);
                    await s.Send(new TracerMessage()).ConfigureAwait(false);
                }))
                .WithEndpoint<DerivedEventSubscriber>(c => c.When(async s =>
                {
                    await s.Subscribe<MyDerivedEvent1>().ConfigureAwait(false);
                    await s.Send(new TracerMessage()).ConfigureAwait(false);
                }))
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
            public bool DerivedEventSubscribed { get; set; } = true;
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.ConfigureBroker().Yankee();
                });
            }

            class TracerHandler : IHandleMessages<TracerMessage>
            {
                Context scenarioContext;

                public TracerHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(TracerMessage message, IMessageHandlerContext context)
                {
                    if (context.MessageHeaders[Headers.OriginatingEndpoint].Contains("BaseEventSubscriber"))
                    {
                        scenarioContext.BaseEventSubscribed = true;
                    }
                    else
                    {
                        scenarioContext.DerivedEventSubscribed = true;
                    }
                    return Task.CompletedTask;
                }
            }
        }

        class BaseEventSubscriber : EndpointConfigurationBuilder
        {
            public BaseEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();

                    c.DisableFeature<AutoSubscribe>();
                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyBaseEvent1), PublisherEndpoint);
                    bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpoint);
                });
            }

            class BaseEventHandler : IHandleMessages<MyBaseEvent1>
            {
                Context scenarioContext;

                public BaseEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyBaseEvent1 message, IMessageHandlerContext context)
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
                    c.ConfigureBroker().Alpha();

                    c.DisableFeature<AutoSubscribe>();
                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyDerivedEvent1), PublisherEndpoint);
                    bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpoint);
                });
            }

            class DerivedEventHandler : IHandleMessages<MyDerivedEvent1>
            {
                Context scenarioContext;

                public DerivedEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyDerivedEvent1 message, IMessageHandlerContext context)
                {
                    scenarioContext.DerivedEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyBaseEvent1 : IEvent
        {
        }

        class MyDerivedEvent1 : MyBaseEvent1
        {
        }

        class TracerMessage : IMessage
        {
        }
    }
}
