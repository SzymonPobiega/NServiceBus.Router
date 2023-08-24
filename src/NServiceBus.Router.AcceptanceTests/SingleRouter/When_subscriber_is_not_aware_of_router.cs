using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_subscriber_is_not_aware_of_router : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Subscribe_message_routing_has_to_be_configured_at_router()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    var left = cfg.AddInterface("Left", false);
                    left.Broker().Alpha();
                    left.InMemorySubscriptions();
                    var right = cfg.AddInterface("Right", false);
                    right.Broker().Bravo();
                    right.InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                    cfg.AddRule(_ => new PublisherRule());
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<Subscriber>()
                .Done(c => c.EventDelivered)
                .Run();

            Assert.IsTrue(result.EventDelivered);
        }

        class PublisherRule : IRule<SubscribePreroutingContext, SubscribePreroutingContext>
        {
            public Task Invoke(SubscribePreroutingContext context, Func<SubscribePreroutingContext, Task> next)
            {
                context.Destinations.Add(new Destination(Conventions.EndpointNamingConvention(typeof(Publisher)), null));
                return next(context);
            }
        }

        class Context : ScenarioContext
        {
            public bool EventDelivered { get; set; }
            public bool EventSubscribed { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.ConfigureBroker().Alpha();

                    var routing = c.ConfigureRouting();
                    routing.EnableMessageDrivenPubSubCompatibilityMode();

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.EventSubscribed = true;
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
                    c.ConfigureBroker().Bravo();

                    var routing = c.ConfigureRouting();

                    //Send Subscribe message to Router
                    routing.EnableMessageDrivenPubSubCompatibilityMode().RegisterPublisher(typeof(MyEvent), "Router");
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
                    return Task.CompletedTask;
                }
            }
        }

        class MyEvent : IEvent
        {
        }
    }
}
