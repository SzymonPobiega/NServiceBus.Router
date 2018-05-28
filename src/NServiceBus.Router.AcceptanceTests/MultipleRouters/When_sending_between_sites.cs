using System;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Router;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.MultipleRouters
{
    [TestFixture]
    public class When_sending_between_sites : NServiceBusAcceptanceTest
    {
        const string ReceiverEndpoint = "SendingBetweenSites.Receiver";

        [Test]
        public async Task Should_deliver_the_reply_back()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Green-Yellow", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Green", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Yellow", t => t.BrokerBravo()).InMemorySubscriptions();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddRoute((i, d) => i == "Green" && d.Endpoint == ReceiverEndpoint && d.Site == "Blue", 
                        $"Endpoint = {ReceiverEndpoint} AND Interface = Green AND Site = Blue",
                        "Yellow-Blue", "Yellow");
                    routeTable.AddRoute((i, d) => i == "Green" && d.Endpoint == ReceiverEndpoint && d.Site == "Red",
                        $"Endpoint = {ReceiverEndpoint} AND Interface = Green AND Site = Red",
                        "Yellow-Red", "Yellow");
                })
                .WithRouter("Yellow-Blue", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Yellow", t => t.BrokerBravo()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerCharlie()).InMemorySubscriptions();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Yellow", "Blue");
                })
                .WithRouter("Yellow-Red", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Yellow", t => t.BrokerBravo()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Red", t => t.BrokerDelta()).InMemorySubscriptions();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Yellow", "Red");
                })
                .WithEndpoint<Sender>(c => c.When(async s =>
                {
                    await SendToSite(s, "Blue").ConfigureAwait(false);
                    await SendToSite(s, "Red").ConfigureAwait(false);
                }))
                .WithEndpoint<BlueReceiver>()
                .WithEndpoint<RedReceiver>()
                .Done(c => c.BlueRequestReceived 
                           && c.BlueResponseReceived 
                           && c.RedRequestReceived 
                           && c.RedResponseReceived)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(result.BlueResponseReceived);
            Assert.IsTrue(result.RedResponseReceived);
        }

        static Task SendToSite(IMessageSession s, string site)
        {
            var ops = new SendOptions();
            ops.SendToSites(site);
            return s.Send(new MyRequest(), ops);
        }

        class Context : ScenarioContext
        {
            public bool RedRequestReceived { get; set; }
            public bool RedResponseReceived { get; set; }
            public bool BlueRequestReceived { get; set; }
            public bool BlueResponseReceived { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    var bridge = routing.ConnectToBridge("Green-Yellow");
                    bridge.RouteToEndpoint(typeof(MyRequest), ReceiverEndpoint);
                });
            }

            class BlueResponseHandler : IHandleMessages<BlueResponse>
            {
                Context scenarioContext;

                public BlueResponseHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(BlueResponse response, IMessageHandlerContext context)
                {
                    scenarioContext.BlueResponseReceived = true;
                    return Task.CompletedTask;
                }
            }

            class RedResponseHandler : IHandleMessages<RedResponse>
            {
                Context scenarioContext;

                public RedResponseHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(RedResponse request, IMessageHandlerContext context)
                {
                    scenarioContext.RedResponseReceived = true;
                    return Task.CompletedTask;
                }
            }
        }

        class BlueReceiver : EndpointConfigurationBuilder
        {
            public BlueReceiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerCharlie();
                }).CustomEndpointName(ReceiverEndpoint);
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
                    scenarioContext.BlueRequestReceived = true;
                    return context.Reply(new BlueResponse());
                }
            }
        }

        class RedReceiver : EndpointConfigurationBuilder
        {
            public RedReceiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerDelta();
                }).CustomEndpointName(ReceiverEndpoint);
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
                    scenarioContext.RedRequestReceived = true;
                    return context.Reply(new RedResponse());
                }
            }
        }

        class MyRequest : IMessage
        {
        }

        class BlueResponse : IMessage
        {
        }

        class RedResponse : IMessage
        {
        }
    }
}
