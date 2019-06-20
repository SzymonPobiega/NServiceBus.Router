using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.Poison
{
    using System;
    using System.Linq;
    using AcceptanceTesting.Customization;
    using Pipeline;

    [TestFixture]
    public class When_reply_does_not_contain_correlation_id : NServiceBusAcceptanceTest
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
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
                .WithEndpoint<Receiver>()
                .WithPosionSpyComponent(t => t.BrokerBravo())
                .Done(c => c.ResponseReceived || c.PoisonMessageDetected)
                .Run();

            Assert.IsFalse(result.ResponseReceived);
            Assert.IsTrue(result.PoisonMessageDetected);
            StringAssert.StartsWith("The reply has to contain either 'NServiceBus.CorrelationId' header", result.ExceptionMessage);
            Assert.IsTrue(result.Logs.Any(l => l.Message.Contains(result.ExceptionMessage)));
        }

        class Context : ScenarioContext, IPoisonSpyContext
        {
            public bool RequestReceived { get; set; }
            public bool ResponseReceived { get; set; }
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
                    c.UseTransport<TestTransport>().BrokerBravo();
                    c.Pipeline.Register(new ClearCorrelationIdBehavior(), "Removes correlation header");
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

            class ClearCorrelationIdBehavior : IBehavior<IDispatchContext, IDispatchContext>
            {
                public Task Invoke(IDispatchContext context, Func<IDispatchContext, Task> next)
                {
                    foreach (var operation in context.Operations)
                    {
                        operation.Message.Headers.Remove(Headers.CorrelationId);
                    }

                    return next(context);
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
