using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System.IO;
    using System.Linq;
    using System.Text;
    using AcceptanceTesting.Customization;
    using global::Newtonsoft.Json;
    using Routing;
    using Transport;

    [TestFixture]
    public class When_mutating_messages : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Receiver_should_see_modified_body()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                    cfg.InterceptForwarding((queue, message, dispatch, forwardMethod) => forwardMethod((ops, transaction, context) =>
                    {
                        var op = ops.UnicastTransportOperations.Single();
                        if (!op.Message.Headers.ContainsKey(Headers.EnclosedMessageTypes))
                        {
                            return dispatch(ops, transaction, context);
                        }

                        MyMessage deserialized;
                        using (var stream = new MemoryStream(op.Message.Body))
                        {
                            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                            {
                                using (var jsonReader = new JsonTextReader(streamReader))
                                {
                                    var serializer = new JsonSerializer();
                                    deserialized = serializer.Deserialize<MyMessage>(jsonReader);
                                }
                            }
                        }

                        var toSerialize = new MyMessage
                        {
                            Number = deserialized.Number + 2
                        };

                        var serialized = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(toSerialize));

                        var newMessage = new OutgoingMessage(op.Message.MessageId, op.Message.Headers, serialized);
                        var newOp = new TransportOperation(newMessage, new UnicastAddressTag(op.Destination), op.RequiredDispatchConsistency, op.DeliveryConstraints);
                        var newOps = new TransportOperations(newOp);

                        return dispatch(newOps, transaction, context);
                    }));
                })
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyMessage
                {
                    Number = 42
                })))
                .WithEndpoint<Receiver>()
                .Done(c => c.ValueReceived != 0)
                .Run();

            Assert.AreEqual(44, result.ValueReceived);
        }

        class Context : ScenarioContext
        {
            public int ValueReceived { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RouteToEndpoint(typeof(MyMessage), Conventions.EndpointNamingConvention(typeof(Receiver)));
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

            class MyRequestHandler : IHandleMessages<MyMessage>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.ValueReceived = message.Number;
                    return Task.CompletedTask;
                }
            }
        }

        class MyMessage : IMessage
        {
            public int Number { get; set; }
        }
    }
}
