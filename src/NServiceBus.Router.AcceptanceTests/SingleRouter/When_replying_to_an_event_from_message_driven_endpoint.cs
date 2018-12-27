using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Features;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_replying_to_an_event_from_message_driven_endpoint : NServiceBusAcceptanceTest
    {
        static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    var left = cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    //To ensure when tracer arrives the subscribe request has already been processed.
                    left.LimitMessageProcessingConcurrencyTo(1);

                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>(c => c.When(async s =>
                {
                    await s.Subscribe<MyEvent>().ConfigureAwait(false);
                }))
                .Done(c => c.ReplyDelivered)
                .Run();

            Assert.IsTrue(result.EventDelivered);
        }

        class Context : ScenarioContext
        {
            public bool EventDelivered { get; set; }
            public bool EventSubscribed { get; set; }
            public bool ReplyDelivered { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.UseTransport<TestTransport>().BrokerAlpha();

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.EventSubscribed = true;
                    });
                });
            }

            class ReplyHandler : IHandleMessages<MyReply>
            {
                Context scenarioContext;

                public ReplyHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyReply message, IMessageHandlerContext context)
                {
                    scenarioContext.ReplyDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.DisableFeature<AutoSubscribe>();
                    var routing = c.UseTransport<TestTransport>().BrokerBravo().Routing();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyEvent), PublisherEndpoint);
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
                    scenarioContext.EventDelivered = true;
                    return context.Reply(new MyReply());
                }
            }
        }

        class MyReply : IMessage
        {
        }

        class MyEvent : IEvent
        {
        }
    }
}
