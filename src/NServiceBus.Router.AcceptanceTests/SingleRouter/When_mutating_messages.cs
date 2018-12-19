using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using System.IO;
    using System.Text;
    using AcceptanceTesting.Customization;
    using global::Newtonsoft.Json;

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
                    cfg.AddRule(_ => new MessageMutator());
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

        class MessageMutator : IRule<PreroutingContext, PreroutingContext>
        {
            static JsonSerializer jsonSerializer = new JsonSerializer();

            public Task Invoke(PreroutingContext context, Func<PreroutingContext, Task> next)
            {
                var message = DeserializeMessage(context.Body, jsonSerializer);
                message.Number += 2;
                context.Body = SerializeMessage(message, jsonSerializer);
                return next(context);
            }

            static MyMessage DeserializeMessage(byte[] body, JsonSerializer serializer)
            {
                MyMessage deserialized;
                using (var stream = new MemoryStream(body))
                {
                    using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                    {
                        using (var jsonReader = new JsonTextReader(streamReader))
                        {
                            deserialized = serializer.Deserialize<MyMessage>(jsonReader);
                        }
                    }
                }
                return deserialized;
            }

            static byte[] SerializeMessage(MyMessage message, JsonSerializer serializer)
            {
                using (var stream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                    {
                        using (var jsonWriter = new JsonTextWriter(streamWriter))
                        {
                            serializer.Serialize(jsonWriter, message);
                        }
                    }
                    return stream.ToArray();
                }
            }
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
