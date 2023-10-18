using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
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
                    cfg.AddInterface("Green", false).Broker().Alpha();
                    cfg.AddInterface("Blue", false).Broker().Bravo();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Green", "Blue", "Blue-Red");
                })
                .WithRouter("Blue-Red", cfg =>
                {
                    cfg.AddInterface("Blue", false).Broker().Bravo();
                    cfg.AddInterface("Red", false).Broker().Charlie();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Blue", "Red", "Red-Yellow");
                })
                .WithRouter("Red-Yellow", cfg =>
                {
                    cfg.AddInterface("Red", false).Broker().Charlie();
                    cfg.AddInterface("Yellow", false).Broker().Delta();

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
                    c.ConfigureBroker().Alpha();

                    var routing = c.ConfigureRouting();
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
                    c.ConfigureBroker().Delta();
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