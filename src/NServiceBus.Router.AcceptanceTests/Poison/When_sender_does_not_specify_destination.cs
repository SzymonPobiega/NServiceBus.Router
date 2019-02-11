using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.Poison
{
    using System.Linq;

    [TestFixture]
    public class When_sender_does_not_specify_destination : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_move_message_to_poison_queue()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                })
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new PoisonMessage())))
                .WithEndpoint<Receiver>()
                .WithPosionSpyComponent(t => t.BrokerAlpha())
                .Done(c => c.PoisonMessageDetected || c.RequestReceived)
                .Run();

            Assert.IsFalse(result.RequestReceived);
            Assert.IsTrue(result.PoisonMessageDetected);
            StringAssert.StartsWith("No terminator handled the message in the SendPreroutingContext", result.ExceptionMessage);
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
                    var router = routing.ConnectToRouter("Router");
                    router.DelegateRouting(typeof(PoisonMessage));
                });
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for reply
                    c.UseTransport<TestTransport>().BrokerBravo();
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
