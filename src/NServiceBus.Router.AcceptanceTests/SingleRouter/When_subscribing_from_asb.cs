#if NET461
namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Configuration.AdvancedExtensibility;
    using Events;
    using Features;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Serialization;
    using Settings;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    public class When_subscribing_from_asb : NServiceBusAcceptanceTest
    {
        static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task It_should_deliver_the_message_even_is_subscriber_name_exceeds_the_asb_limit()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", (c, cfg) =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    var leftIface = cfg.AddInterface<AzureServiceBusTransport>("Right", t =>
                    {
                        var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
                        t.ConnectionString(connString);
                        t.Transactions(TransportTransactionMode.ReceiveOnly);

                        t.UseForwardingTopology();
                        t.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();
                    });
                    leftIface.LimitMessageProcessingConcurrencyTo(1); //To ensure when tracer arrives the subscribe request has already been processed.;
                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyAsbEvent())))
                .WithEndpoint<Subscriber>(c => c.When(async s =>
                {
                    await s.Subscribe<MyAsbEvent>().ConfigureAwait(false);
                }))
                .Done(c => c.EventDelivered)
                .Run();

            Assert.IsTrue(result.EventDelivered);
        }

        static bool EventConvention(Type x)
        {
            return x.Namespace == "Events";
        }

        class Context : ScenarioContext
        {
            public bool EventDelivered { get; set; }
            public bool EventSubscribed { get; set; }
        }

        class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //No bridge configuration needed for publisher
                    c.UseTransport<TestTransport>().BrokerAlpha();
                    c.LimitMessageProcessingConcurrencyTo(1);
                    c.Conventions().DefiningEventsAs(EventConvention);
                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.EventSubscribed = true;
                    });
                });
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.DisableFeature<AutoSubscribe>();

                    var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
                    var transport = c.UseTransport<AzureServiceBusTransport>();
                    transport.ConnectionString(connString);
                    transport.UseForwardingTopology();
                    transport.Sanitization().UseStrategy<ValidateAndHashIfNeeded>();

                    var router = transport.Routing().ConnectToRouter("Router");
                    router.RegisterPublisher(typeof(MyAsbEvent), PublisherEndpointName);

                    c.Conventions().DefiningEventsAs(EventConvention);
                }).CustomEndpointName("SubscriberWithAVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongName");
            }

            class MyAsbEventHandler : IHandleMessages<MyAsbEvent>
            {
                Context scenarioContext;

                public MyAsbEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyAsbEvent message, IMessageHandlerContext context)
                {
                    scenarioContext.EventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }
    }
}
#endif
