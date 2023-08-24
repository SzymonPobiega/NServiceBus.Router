using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_not_all_interfaces_are_interested_in_an_event : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_forward_the_message_only_to_interested_interfaces()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface("Red", false).Broker().Alpha();
                    cfg.AddInterface("Green", false).Broker().Bravo();
                    
                    //Charlie broker is not interested in the event
                    cfg.AddInterface("Blue", false).Broker().Charlie();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Green", "Red");
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyEvent())))
                .WithEndpoint<BaseEventSubscriber>()
                .Done(c => c.EventDelivered)
                .Run();

            Assert.IsTrue(result.EventDelivered);
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
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.EventSubscribed = true;
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
                    bridge.RegisterPublisher(typeof(MyEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
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
