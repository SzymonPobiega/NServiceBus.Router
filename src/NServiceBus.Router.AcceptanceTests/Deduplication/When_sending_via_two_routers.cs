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
    public class When_sending_via_two_routers : NServiceBusAcceptanceTest
    {
        const string ConnectionString = "data source = (local); initial catalog=test1; integrated security=true";

        [Test]
        public async Task Should_deliver_the_reply_back()
        {
            await Scenario.Define<Context>()
                .WithRouter("Green-Blue", cfg =>
                {
                    var deduplicationConfig = cfg.ConfigureDeduplication();
#pragma warning disable 618
                    deduplicationConfig.EnableInstaller(true);
#pragma warning restore 618
                    var linkInterface = cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();

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

                    var linkInterface = cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();

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
                .WithEndpoint<GreenEndpoint>(c => c.When(s => s.Send(new GreenRequest
                {
                    Counter = 0
                })))
                .WithEndpoint<RedEndpoint>()
                .Done(c => c.Counter > 20)
                .Run(TimeSpan.FromSeconds(60));
        }

        class Context : ScenarioContext
        {
            int counter;

            public int Counter => Volatile.Read(ref counter);

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
                    var incremented = scenarioContext.IncrementCounter();
                    return context.Send(new GreenRequest
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
                });
            }

            class GreenRequestHandler : IHandleMessages<GreenRequest>
            {
                public Task Handle(GreenRequest request, IMessageHandlerContext context)
                {
                    return context.Reply(new GreenResponse
                    {
                        Counter = request.Counter
                    });
                }
            }
        }

        class GreenRequest : IMessage
        {
            public int Counter { get; set; }

            public override string ToString()
            {
                return Counter.ToString();
            }
        }

        class GreenResponse : IMessage
        {
            public int Counter { get; set; }

            public override string ToString()
            {
                return Counter.ToString();
            }
        }
    }
}
