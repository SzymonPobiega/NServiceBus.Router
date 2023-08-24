using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System.Collections.Generic;
    using System.Threading;
    using Routing;

    [TestFixture]
    public class When_receiver_is_scaled_out : NServiceBusAcceptanceTest
    {
        static readonly string ReceiverEndpointName = "ReceiverIsScaledOut.Receiver";

        [Test]
        public async Task Should_deliver_messages_to_all_instances()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface("Left", false).Broker().Alpha();
                    var right = cfg.AddInterface("Right", false);
                    right.Broker().Bravo();
                    right.EndpointInstances.AddOrReplaceInstances("config",
                        new List<EndpointInstance>
                        {
                            new EndpointInstance(ReceiverEndpointName, "A"),
                            new EndpointInstance(ReceiverEndpointName, "B")
                        });

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                })
                .WithEndpoint<Sender>(s => s.When(async (session, ctx) =>
                {
                    while (!ctx.RequestReceivedByA || !ctx.RequestReceivedByB)
                    {
                        await session.Send(new MyRequest()).ConfigureAwait(false);
                        await ctx.SendSemaphore.WaitAsync().ConfigureAwait(false);
                    }
                }))
                .WithEndpoint<ReceiverA>()
                .WithEndpoint<ReceiverB>()
                .Done(c => c.RequestReceivedByA && c.RequestReceivedByB)
                .Run();

            Assert.IsTrue(result.RequestReceivedByA);
            Assert.IsTrue(result.RequestReceivedByB);
        }

        class Context : ScenarioContext
        {
            public bool RequestReceivedByA { get; set; }
            public bool RequestReceivedByB { get; set; }
            public SemaphoreSlim SendSemaphore { get; } = new SemaphoreSlim(1);
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();

                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RouteToEndpoint(typeof(MyRequest), ReceiverEndpointName);
                });
            }
        }

        class ReceiverA : EndpointConfigurationBuilder
        {
            public ReceiverA()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        c.ConfigureBroker().Bravo();
                        c.MakeInstanceUniquelyAddressable("A");
                    })
                    .CustomEndpointName(ReceiverEndpointName);
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
                    scenarioContext.RequestReceivedByA = true;
                    scenarioContext.SendSemaphore.Release();
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
                        c.ConfigureBroker().Bravo();
                        c.MakeInstanceUniquelyAddressable("B");
                    })
                    .CustomEndpointName(ReceiverEndpointName);
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
                    scenarioContext.RequestReceivedByB = true;
                    scenarioContext.SendSemaphore.Release();
                    return Task.CompletedTask;
                }
            }
        }

        class MyRequest : IMessage
        {
        }
    }
}
