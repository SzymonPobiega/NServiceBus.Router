using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Router;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_using_sql_persistence : NServiceBusAcceptanceTest
    {
        static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            await GetSubscriptionStorage("A").Install().ConfigureAwait(false);
            await GetSubscriptionStorage("D").Install().ConfigureAwait(false);

            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface<TestTransport>("A", t => t.BrokerAlpha()).UseSubscriptionPersistence(GetSubscriptionStorage("A"));
                    cfg.AddInterface<TestTransport>("D", t => t.BrokerBravo()).UseSubscriptionPersistence(GetSubscriptionStorage("D"));

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("D", "A");
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent())))
                .WithEndpoint<BaseEventSubscriber>()
                .WithEndpoint<DerivedEventSubscriber>()
                .Done(c => c.BaseEventDelivered && c.DerivedEventDeilvered)
                .Run();

            Assert.IsTrue(result.BaseEventDelivered);
            Assert.IsTrue(result.DerivedEventDeilvered);
        }

        static SqlSubscriptionStorage GetSubscriptionStorage(string tablePrefix)
        {
            var storage = new SqlSubscriptionStorage(
                () => new SqlConnection(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"),
                tablePrefix, new SqlDialect.MsSqlServer(), null);
            return storage;
        }

        class Context : ScenarioContext
        {
            public bool BaseEventDelivered { get; set; }
            public bool DerivedEventDeilvered { get; set; }
            public bool BaseEventSubscribed { get; set; }
            public bool DerivedEventSubscribed { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.UseTransport<TestTransport>().BrokerAlpha();

                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        if (args.MessageType.Contains("MyBaseEvent"))
                        {
                            context.BaseEventSubscribed = true;
                        }
                        else
                        {
                            context.DerivedEventSubscribed = true;
                        }
                    });
                });
            }
        }

        class BaseEventSubscriber : EndpointConfigurationBuilder
        {
            public BaseEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerBravo().Routing();
                    var bridge = routing.ConnectToBridge("Router");
                    bridge.RegisterPublisher(typeof(MyBaseEvent), PublisherEndpoint);
                });
            }

            class BaseEventHandler : IHandleMessages<MyBaseEvent>
            {
                Context scenarioContext;

                public BaseEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyBaseEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.BaseEventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class DerivedEventSubscriber : EndpointConfigurationBuilder
        {
            public DerivedEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerBravo().Routing();
                    var bridge = routing.ConnectToBridge("Router");
                    bridge.RegisterPublisher(typeof(MyDerivedEvent), PublisherEndpoint);
                });
            }

            class DerivedEventHandler : IHandleMessages<MyDerivedEvent>
            {
                Context scenarioContext;

                public DerivedEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyDerivedEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.DerivedEventDeilvered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyBaseEvent : IEvent
        {
        }

        class MyDerivedEvent : MyBaseEvent
        {
        }
    }
}
