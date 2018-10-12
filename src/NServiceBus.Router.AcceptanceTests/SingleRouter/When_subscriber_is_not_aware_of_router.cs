using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
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
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

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
                    c.UseTransport<TestTransport>().BrokerAlpha();

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
                    var routing = c.UseTransport<TestTransport>().BrokerBravo().Routing();
                    //Send Subscribe message to Router
                    routing.RegisterPublisher(typeof(MyEvent), "Router");
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
