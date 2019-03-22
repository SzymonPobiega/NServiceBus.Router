using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_sending_a_message_based_on_other_message : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_unwrap_correlation_id_when_sending_the_second_message()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                })
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
                .WithEndpoint<Receiver>()
                .Done(c => c.RequestReceived && c.ResponseReceived)
                .Run();

            Assert.IsTrue(result.RequestReceived);
            Assert.IsTrue(result.ResponseReceived);

            //Ensure the correlation ID header to not grow forever
            Assert.AreEqual(112, result.ReceivedCorrelationId.Length);
        }

        class Context : ScenarioContext
        {
            public bool RequestReceived { get; set; }
            public bool ResponseReceived { get; set; }
            public string ReceivedCorrelationId { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    var bridge = routing.ConnectToRouter("Router");
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
                    scenarioContext.ReceivedCorrelationId = context.MessageHeaders[Headers.CorrelationId];
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
                    var routing = c.UseTransport<TestTransport>().BrokerBravo().Routing();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RouteToEndpoint(typeof(MyResponse), Conventions.EndpointNamingConvention(typeof(Sender)));
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
                    return context.Send(new MyResponse());
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
