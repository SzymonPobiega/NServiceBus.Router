using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System.Threading;
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_subscriber_is_scaled_out : NServiceBusAcceptanceTest
    {
        static readonly string SubscriberEndpointName = "SubscriberIsScaledOut.Subscriber";

        [Test]
        public async Task Should_deliver_messages_to_all_instances()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface("Left", false).Broker().Alpha();
                    cfg.AddInterface("Right", false).Broker().Bravo();
                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                })
                .WithEndpoint<Publisher>(s => s.When(ctx => ctx.Subscribed, async (session, ctx) =>
                {
                    while (!ctx.EventReceivedByA || !ctx.EventReceivedByB)
                    {
                        await session.Publish(new MyEvent()).ConfigureAwait(false);
                        await ctx.PublishSemaphore.WaitAsync().ConfigureAwait(false);
                    }
                }))
                .WithEndpoint<SubscriberA>()
                .WithEndpoint<SubscriberB>()
                .Done(c => c.EventReceivedByA && c.EventReceivedByB)
                .Run();

            Assert.IsTrue(result.EventReceivedByA);
            Assert.IsTrue(result.EventReceivedByB);
        }

        class Context : ScenarioContext
        {
            public bool EventReceivedByA { get; set; }
            public bool EventReceivedByB { get; set; }
            public SemaphoreSlim PublishSemaphore { get; } = new SemaphoreSlim(1);
            public bool Subscribed { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.Subscribed = true;
                    });
                });
            }
        }

        class SubscriberA : EndpointConfigurationBuilder
        {
            public SubscriberA()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        c.ConfigureBroker().Bravo();

                        var routing = c.ConfigureRouting();
                        var bridge = routing.ConnectToRouter("Router");
                        bridge.RegisterPublisher(typeof(MyEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));

                        c.MakeInstanceUniquelyAddressable("A");
                    })
                    .CustomEndpointName(SubscriberEndpointName);
            }

            class MyRequestHandler : IHandleMessages<MyEvent>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    scenarioContext.EventReceivedByA = true;
                    scenarioContext.PublishSemaphore.Release();
                    return Task.CompletedTask;
                }
            }
        }

        class SubscriberB : EndpointConfigurationBuilder
        {
            public SubscriberB()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        c.ConfigureBroker().Bravo();
                        var routing = c.ConfigureRouting();
                        var bridge = routing.ConnectToRouter("Router");
                        bridge.RegisterPublisher(typeof(MyEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));

                        c.MakeInstanceUniquelyAddressable("B");
                    })
                    .CustomEndpointName(SubscriberEndpointName);
            }

            class MyRequestHandler : IHandleMessages<MyEvent>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    scenarioContext.EventReceivedByB = true;
                    scenarioContext.PublishSemaphore.Release();
                    return Task.CompletedTask;
                }
            }
        }

        class MyEvent : IEvent
        {
        }
    }
}
