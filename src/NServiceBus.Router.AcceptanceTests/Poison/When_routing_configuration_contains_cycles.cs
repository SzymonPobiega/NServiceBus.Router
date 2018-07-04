using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.Poison
{
    using System.Linq;
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_routing_configuration_contains_cycles : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_move_message_to_poison_queue()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Green-Blue", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Green", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Green", "Blue", "Blue-Green");
                })
                .WithRouter("Blue-Green", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Green", t => t.BrokerAlpha()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Blue", "Green", "Green-Blue");
                })
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new PoisonMessage())))
                .WithEndpoint<Receiver>()
                .WithPosionSpyComponent(t => t.BrokerAlpha())
                .WithPosionSpyComponent(t => t.BrokerBravo())
                .Done(c => c.PoisonMessageDetected || c.RequestReceived)
                .Run();

            Assert.IsFalse(result.RequestReceived);
            Assert.IsTrue(result.PoisonMessageDetected);
            Assert.AreEqual("Routing cycle detected: via|10|Green-Blue|via|10|Blue-Green", result.ExceptionMessage);
            Assert.IsTrue(result.Logs.Any(l => l.Message.Contains(result.ExceptionMessage)));
        }

        class Context : ScenarioContext, IPoisonSpyContext
        {
            public bool RequestReceived { get; set; }
            public string ExceptionMessage { get; set; }
            public bool PoisonMessageDetected { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    var router = routing.ConnectToRouter("Green-Blue");
                    router.RouteToEndpoint(typeof(PoisonMessage), Conventions.EndpointNamingConvention(typeof(Receiver)));
                });
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerCharlie();
                });
            }

            class MyRequestHandler : IHandleMessages<PoisonMessage>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(PoisonMessage request, IMessageHandlerContext context)
                {
                    scenarioContext.RequestReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        class PoisonMessage : IMessage
        {
        }
    }
}
