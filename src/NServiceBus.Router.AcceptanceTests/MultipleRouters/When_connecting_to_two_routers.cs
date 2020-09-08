using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.MultipleRouters
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_connecting_to_two_routers : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_the_messages_to_destination_endpoints()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("RouterA", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerCharlie()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerAlpha()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                })
                .WithRouter("RouterB", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerCharlie()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                })
                .WithEndpoint<Sender>(c => c.When(async s =>
                {
                    await s.Send(new MyRequestA());
                    await s.Send(new MyRequestB());
                }))
                .WithEndpoint<ReceiverA>()
                .WithEndpoint<ReceiverB>()
                .Done(c => c.ReceivedByB && c.ReceivedByA)
                .Run();

            Assert.IsTrue(result.ReceivedByB);
            Assert.IsTrue(result.ReceivedByA);
        }

        class Context : ScenarioContext
        {
            public bool ReceivedByA { get; set; }
            public bool ReceivedByB { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerCharlie().Routing();

                    var routerA = routing.ConnectToRouter("RouterA");
                    routerA.RouteToEndpoint(typeof(MyRequestA), Conventions.EndpointNamingConvention(typeof(ReceiverA)));

                    var routerB = routing.ConnectToRouter("RouterB");
                    routerB.RouteToEndpoint(typeof(MyRequestB), Conventions.EndpointNamingConvention(typeof(ReceiverB)));
                });
            }
        }

        class ReceiverA : EndpointConfigurationBuilder
        {
            public ReceiverA()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for reply
                    c.UseTransport<TestTransport>().BrokerAlpha();
                });
            }

            class MyRequestHandler : IHandleMessages<MyRequestA>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyRequestA requestA, IMessageHandlerContext context)
                {
                    scenarioContext.ReceivedByA = true;
                    return Task.CompletedTask;
                }
            }
        }

        class ReceiverB : EndpointConfigurationBuilder
        {
            public ReceiverB()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for reply
                    c.UseTransport<TestTransport>().BrokerBravo();
                });
            }

            class MyRequestHandler : IHandleMessages<MyRequestB>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyRequestB requestB, IMessageHandlerContext context)
                {
                    scenarioContext.ReceivedByB = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyRequestA : IMessage
        {
        }

        class MyRequestB : IMessage
        {
        }
    }
}
