using System;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.Deduplication
{
    using System.Data.SqlClient;
    using System.Threading;
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_publishing_via_two_routers_native_pubsub : NServiceBusAcceptanceTest
    {
        const string ConnectionString = "data source = (local); initial catalog=test1; integrated security=true";

        [Test]
        public async Task Should_deliver_the_event_in_message_driven_mode()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Green-Blue", cfg =>
                {
                    var deduplicationConfig = cfg.ConfigureDeduplication();
#pragma warning disable 618
                    deduplicationConfig.EnableInstaller(true);
#pragma warning restore 618
                    var linkInterface = cfg.AddInterface<TestTransport>("Blue", t => t.BrokerYankee());
                    //linkInterface.DisableNativePubSub();
                    linkInterface.EnableMessageDrivenPublishSubscribe(new InMemorySubscriptionStorage());

                    var sqlInterface = cfg.AddInterface<SqlServerTransport>("Green", t =>
                    {
                        t.ConnectionString(ConnectionString);
                        t.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
                    });
                    sqlInterface.InMemorySubscriptions();
                    sqlInterface.EnableDeduplication(linkInterface.Name, "Red-Blue", () => new SqlConnection(ConnectionString), 10);

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Blue", "Green");
                    routeTable.AddForwardRoute("Green", "Blue", "Red-Blue");
                })
                .WithRouter("Red-Blue", cfg =>
                {
                    var deduplicationConfig = cfg.ConfigureDeduplication();
#pragma warning disable 618
                    deduplicationConfig.EnableInstaller(true);
#pragma warning restore 618

                    var linkInterface = cfg.AddInterface<TestTransport>("Blue", t => t.BrokerYankee());
                    //linkInterface.DisableNativePubSub();
                    linkInterface.EnableMessageDrivenPublishSubscribe(new InMemorySubscriptionStorage());

                    var sqlInterface = cfg.AddInterface<SqlServerTransport>("Red", t =>
                    {
                        t.ConnectionString(ConnectionString);
                        t.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
                    });
                    sqlInterface.InMemorySubscriptions();
                    sqlInterface.EnableDeduplication(linkInterface.Name, "Green-Blue", () => new SqlConnection(ConnectionString), 5);

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Blue", "Red");
                    routeTable.AddForwardRoute("Red", "Blue", "Green-Blue");
                })
                .WithEndpoint<GreenEndpoint>(c => c.When(ctx => ctx.EventSubscribed, s => s.Send(new MyMessage
                {
                    Counter = 0
                })))
                .WithEndpoint<RedEndpoint>()
                .Done(c => c.Counter > 5)
                .Run(TimeSpan.FromSeconds(60));

            Assert.IsTrue(result.AreSendsDeduplicated);
            Assert.IsTrue(result.ArePublishesDeduplicated);
        }

        class Context : ScenarioContext
        {
            int counter;

            public int Counter => Volatile.Read(ref counter);
            public bool EventSubscribed { get; set; }
            public bool AreSendsDeduplicated { get; set; }
            public bool ArePublishesDeduplicated { get; set; }

            public int IncrementCounter()
            {
                return Interlocked.Increment(ref counter);
            }
        }

        class GreenEndpoint : EndpointConfigurationBuilder
        {
            public GreenEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.UseTransport<SqlServerTransport>();
                    transport.ConnectionString(ConnectionString);
                    var bridge = transport.Routing().ConnectToRouter("Green-Blue");
                    bridge.RouteToEndpoint(typeof(MyMessage), Conventions.EndpointNamingConvention(typeof(RedEndpoint)));
                    bridge.RegisterPublisher(typeof(MyEvent), Conventions.EndpointNamingConvention(typeof(RedEndpoint)));
                });
            }

            class MyEventHandler : IHandleMessages<MyEvent>
            {
                Context scenarioContext;

                public MyEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyEvent response, IMessageHandlerContext context)
                {
                    scenarioContext.ArePublishesDeduplicated = context.MessageHeaders.ContainsKey(RouterDeduplicationHeaders.SequenceNumber);

                    var incremented = scenarioContext.IncrementCounter();
                    return context.Send(new MyMessage
                    {
                        Counter = incremented
                    });
                }
            }
        }

        class RedEndpoint : EndpointConfigurationBuilder
        {
            public RedEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.UseTransport<SqlServerTransport>();
                    transport.ConnectionString(ConnectionString);

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.EventSubscribed = true;
                    });
                });
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context scenarioContext;

                public MyMessageHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage request, IMessageHandlerContext context)
                {
                    scenarioContext.AreSendsDeduplicated = context.MessageHeaders.ContainsKey(RouterDeduplicationHeaders.SequenceNumber);

                    return context.Publish(new MyEvent
                    {
                        Counter = request.Counter
                    });
                }
            }
        }

        class MyMessage : ICommand
        {
            public int Counter { get; set; }

            public override string ToString()
            {
                return Counter.ToString();
            }
        }

        class MyEvent : IEvent
        {
            public int Counter { get; set; }

            public override string ToString()
            {
                return Counter.ToString();
            }
        }
    }
}
