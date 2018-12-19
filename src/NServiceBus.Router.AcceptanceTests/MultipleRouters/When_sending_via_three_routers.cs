using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.MultipleRouters
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_sending_via_three_routers : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_the_reply()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Green-Blue", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Green", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Green", "Blue", "Blue-Red");
                })
                .WithRouter("Blue-Red", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Red", t => t.BrokerCharlie()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Blue", "Red", "Red-Yellow");
                })
                .WithRouter("Red-Yellow", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Red", t => t.BrokerCharlie()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Yellow", t => t.BrokerDelta()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Red", "Yellow");
                })
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
                .WithEndpoint<Receiver>()
                .Done(c => c.RequestReceived && c.ResponseReceived)
                .Run();

            Assert.IsTrue(result.RequestReceived);
            Assert.IsTrue(result.ResponseReceived);
        }

        class Context : ScenarioContext
        {
            public bool RequestReceived { get; set; }
            public bool ResponseReceived { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    var bridge = routing.ConnectToRouter("Green-Blue");
                    bridge.RouteToEndpoint(typeof(MyRequest), Conventions.EndpointNamingConvention(typeof(Receiver)));
                });
            }

            class MyResponseHandler : IHandleMessages<MyResponse>
            {
                Context scenarioContext;

                public MyResponseHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyResponse response, IMessageHandlerContext context)
                {
                    scenarioContext.ResponseReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for reply
                    c.UseTransport<TestTransport>().BrokerDelta();
                });
            }

            class MyRequestHandler : IHandleMessages<MyRequest>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyRequest request, IMessageHandlerContext context)
                {
                    scenarioContext.RequestReceived = true;
                    return context.Reply(new MyResponse());
                }
            }
        }

        class MyRequest : IMessage
        {
        }

        class MyResponse : IMessage
        {
        }
    }
}