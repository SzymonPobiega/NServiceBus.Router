using System;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.MultipleRouters
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_sending_via_two_routers : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_the_reply_back()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Green-Blue", cfg =>
                {
                    cfg.AddInterface("Green", false).Broker().Alpha();
                    cfg.AddInterface("Blue", false).Broker().Bravo();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Blue", "Green");
                    routeTable.AddForwardRoute("Green", "Blue", "Red-Blue");
                })
                .WithRouter("Red-Blue", cfg =>
                {
                    cfg.AddInterface("Blue", false).Broker().Bravo();
                    cfg.AddInterface("Red", false).Broker().Charlie();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Blue", "Red");
                    routeTable.AddForwardRoute("Red", "Blue", "Green-Blue");
                })
                .WithEndpoint<GreenEndpoint>(c => c.When(s =>
                {
                    var ops = new SendOptions();
                    //ops.SendToSites("Red");
                    return s.Send(new GreenRequest(), ops);
                }))
                .WithEndpoint<RedEndpoint>(c => c.When(s =>
                {
                    var ops = new SendOptions();
                    //ops.SendToSites("Green");
                    return s.Send(new RedRequest(), ops);
                }))
                .Done(c => c.GreenRequestReceived 
                           && c.GreenResponseReceived 
                           && c.RedRequestReceived 
                           && c.RedResponseReceived)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(result.GreenRequestReceived);
            Assert.IsTrue(result.GreenResponseReceived);
        }

        class Context : ScenarioContext
        {
            public bool GreenRequestReceived { get; set; }
            public bool GreenResponseReceived { get; set; }
            public bool RedRequestReceived { get; set; }
            public bool RedResponseReceived { get; set; }
        }

        class GreenEndpoint : EndpointConfigurationBuilder
        {
            public GreenEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();

                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Green-Blue");
                    bridge.RouteToEndpoint(typeof(GreenRequest), Conventions.EndpointNamingConvention(typeof(RedEndpoint)));
                });
            }

            class GreenResponseHandler : IHandleMessages<GreenResponse>
            {
                Context scenarioContext;

                public GreenResponseHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(GreenResponse response, IMessageHandlerContext context)
                {
                    scenarioContext.GreenResponseReceived = true;
                    return Task.CompletedTask;
                }
            }

            class RedRequestHandler : IHandleMessages<RedRequest>
            {
                Context scenarioContext;

                public RedRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(RedRequest request, IMessageHandlerContext context)
                {
                    scenarioContext.RedRequestReceived = true;
                    return context.Reply(new RedResponse());
                }
            }
        }

        class RedEndpoint : EndpointConfigurationBuilder
        {
            public RedEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Charlie();

                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Red-Blue");
                    bridge.RouteToEndpoint(typeof(RedRequest), Conventions.EndpointNamingConvention(typeof(GreenEndpoint)));
                });
            }

            class RedResponseHandler : IHandleMessages<RedResponse>
            {
                Context scenarioContext;

                public RedResponseHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(RedResponse response, IMessageHandlerContext context)
                {
                    scenarioContext.RedResponseReceived = true;
                    return Task.CompletedTask;
                }
            }

            class GreenRequestHandler : IHandleMessages<GreenRequest>
            {
                Context scenarioContext;

                public GreenRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(GreenRequest request, IMessageHandlerContext context)
                {
                    scenarioContext.GreenRequestReceived = true;
                    return context.Reply(new GreenResponse());
                }
            }
        }

        class GreenRequest : IMessage
        {
        }

        class GreenResponse : IMessage
        {
        }

        class RedRequest : IMessage
        {
        }

        class RedResponse : IMessage
        {
        }
    }
}
