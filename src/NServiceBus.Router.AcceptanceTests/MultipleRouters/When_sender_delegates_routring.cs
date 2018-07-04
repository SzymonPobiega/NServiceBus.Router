using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.MultipleRouters
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_sender_delegates_routring : NServiceBusAcceptanceTest
    {
        static string ReceiverEndpoint => Conventions.EndpointNamingConvention(typeof(Receiver));

        [Test]
        public async Task Should_use_custom_destination_finder_on_destinations_router()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Green-Blue", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Green", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddRoute((iface, destination) => destination.Site == "MySite", "Site = MySite", "Blue-Red", "Blue");
                })
                .WithRouter("Blue-Red", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Red", t => t.BrokerCharlie()).InMemorySubscriptions();

                    var staticRouting = cfg.UseStaticRoutingProtocol();
                    staticRouting.AddForwardRoute("Blue", "Red");
                    cfg.FindDestinationsUsing(message => new [] {new Destination(ReceiverEndpoint, null) });
                })
                .WithEndpoint<Sender>(c => c.When(s =>
                {
                    var ops = new SendOptions();
                    ops.SendToSites("MySite");
                    return s.Send(new MyRequest(), ops);
                }))
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
                    var router = routing.ConnectToRouter("Green-Blue");
                    router.DelegateRouting(typeof(MyRequest));
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
                    c.UseTransport<TestTransport>().BrokerCharlie();
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
