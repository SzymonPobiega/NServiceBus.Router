using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    /// <summary>
    /// One event is subscribed via router and the other is subscribed locally
    /// </summary>
    [TestFixture]
    public class When_subscribing_locally_and_via_router : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_receive_both_events()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface("Left", false).Broker().Alpha();
                    cfg.AddInterface("Right", false).Broker().Bravo();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                })
                .WithEndpoint<LocalPublisher>(c => c.When(x => x.LocalEventSubscribed, s => s.Publish(new LocalEvent())))
                .WithEndpoint<RemotePublisher>(c => c.When(x => x.RemoteEventSubscribed, s => s.Publish(new RemoteEvent())))
                .WithEndpoint<Subscriber>()
                .Done(c => c.LocalEventDelivered && c.RemoteEventDelivered)
                .Run();

            Assert.IsTrue(result.LocalEventDelivered);
            Assert.IsTrue(result.RemoteEventDelivered);
        }

        class Context : ScenarioContext
        {
            public bool LocalEventDelivered { get; set; }
            public bool RemoteEventDelivered { get; set; }
            public bool LocalEventSubscribed { get; set; }
            public bool RemoteEventSubscribed { get; set; }
        }

        class LocalPublisher : EndpointConfigurationBuilder
        {
            public LocalPublisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.ConfigureBroker().Alpha();
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        if (args.MessageType.Contains("LocalEvent"))
                        {
                            context.LocalEventSubscribed = true;
                        }
                        else
                        {
                            context.RemoteEventSubscribed = true;
                        }
                    });
                });
            }
        }

        class RemotePublisher : EndpointConfigurationBuilder
        {
            public RemotePublisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.ConfigureBroker().Bravo();
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        if (args.MessageType.Contains("LocalEvent"))
                        {
                            context.LocalEventSubscribed = true;
                        }
                        else
                        {
                            context.RemoteEventSubscribed = true;
                        }
                    });
                });
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();
                    var routing = c.ConfigureRouting();
                    var routingCompat = routing.EnableMessageDrivenPubSubCompatibilityMode();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(RemoteEvent), Conventions.EndpointNamingConvention(typeof(RemotePublisher)));
                    routingCompat.RegisterPublisher(typeof(LocalEvent), Conventions.EndpointNamingConvention(typeof(LocalPublisher)));
                });
            }

            class LocalEventHandler : IHandleMessages<LocalEvent>
            {
                Context scenarioContext;

                public LocalEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(LocalEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.LocalEventDelivered = true;
                    return Task.CompletedTask;
                }
            }

            class RemoteEventHandler : IHandleMessages<RemoteEvent>
            {
                Context scenarioContext;

                public RemoteEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(RemoteEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.RemoteEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }


        class LocalEvent : IEvent
        {
        }

        class RemoteEvent : IEvent
        {
        }
    }
}
