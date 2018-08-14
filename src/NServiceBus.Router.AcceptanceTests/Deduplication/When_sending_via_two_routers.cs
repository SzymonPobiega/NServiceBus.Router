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
    using NServiceBus.Router.Deduplication;

    [TestFixture]
    public class When_sending_via_two_routers : NServiceBusAcceptanceTest
    {
        const string ConnectionString = "data source = (local); initial catalog=test1; integrated security=true";

        [Test]
        public async Task Should_deliver_the_reply_back()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Green-Blue", cfg =>
                {
                    cfg.EnableSqlDeduplication(c =>
                    {
                        c.ConnectionFactory(() => new SqlConnection(ConnectionString));
                        c.EnsureTotalOrderOfOutgoingMessages("Blue", "Red-Blue");
                        c.EpochSize(10);
                    });

                    cfg.AddInterface<SqlServerTransport>("Green", t =>
                    {
                        t.ConnectionString(ConnectionString);
                        t.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
                    }).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Blue", "Green");
                    routeTable.AddForwardRoute("Green", "Blue", "Red-Blue");
                })
                .WithRouter("Red-Blue", cfg =>
                {
                    cfg.AddInterface<TestTransport>("Blue", t => t.BrokerBravo()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Red", t => t.BrokerCharlie()).InMemorySubscriptions();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("Blue", "Red");
                    routeTable.AddForwardRoute("Red", "Blue", "Green-Blue");
                })
                .WithEndpoint<GreenEndpoint>(c => c.When(s => s.Send(new GreenRequest())))
                .WithEndpoint<RedEndpoint>()
                .Done(c => c.Counter > 50)
                .Run(TimeSpan.FromSeconds(60));
        }

        class Context : ScenarioContext
        {
            public int Counter;
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
                    Interlocked.Increment(ref scenarioContext.Counter);
                    return context.Send(new GreenRequest());
                }
            }
        }

        class RedEndpoint : EndpointConfigurationBuilder
        {
            public RedEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerCharlie();
                });
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
    }
}
