﻿#if NET461
namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using AcceptanceTesting;
    using Events;
    using Features;
    using NServiceBus;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    public class When_publishing_from_asb_endpoint_oriented_while_router_is_restarted : NServiceBusAcceptanceTest
    {
        InMemorySubscriptionStorage subscriptionStorage = new InMemorySubscriptionStorage();
        static string PublisherEndpointName => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", ConfigureRouter)
                .WithEndpoint<Publisher>(c => c.When(x => x.EventSubscribed, s => s.Publish(new MyAsbEvent())))
                .WithEndpoint<Subscriber>(c => c.When(async s =>
                {
                    await s.Subscribe<MyAsbEvent>().ConfigureAwait(false);
                    await s.Send(new TracerMessage()).ConfigureAwait(false);
                }))
                .Done(c => c.EventDelivered)
                .Run();

            Assert.IsTrue(result.EventDelivered);
            Console.WriteLine("Restarting router");

            result = await Scenario.Define<Context>()
                .WithRouter("Router", ConfigureRouter)
                .WithEndpoint<Publisher>(c => c.When(x => x.EndpointsStarted, s => s.Publish(new MyAsbEvent())))
                .WithEndpoint<Subscriber>()
                .Done(c => c.EventDelivered)
                .Run();

            Assert.IsTrue(result.EventDelivered);
        }

        void ConfigureRouter(Context c, RouterConfiguration cfg)
        {
            cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).EnableMessageDrivenPublishSubscribe(subscriptionStorage);
            var leftIface = cfg.AddInterface<AzureServiceBusTransport>("Right", t =>
            {
                var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
                t.ConnectionString(connString);

                var topology = t.UseEndpointOrientedTopology();
                topology.EnableMigrationToForwardingTopology();
                topology.RegisterPublisher(typeof(MyAsbEvent), Conventions.EndpointNamingConvention(typeof(Publisher)));
            });
            leftIface.LimitMessageProcessingConcurrencyTo(1); //To ensure when tracer arrives the subscribe request has already been processed.;
            cfg.AddRule(_ => new SuppressTransactionScopeRule());
            cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
        }

        class SuppressTransactionScopeRule : IRule<RawContext, RawContext>
        {
            public Task Invoke(RawContext context, Func<RawContext, Task> next)
            {
                using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                {
                    return next(context);
                }
            }
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
                    var connString = Environment.GetEnvironmentVariable("AzureServiceBus.ConnectionString");
                    var transport = c.UseTransport<AzureServiceBusTransport>();
                    transport.ConnectionString(connString);
                    transport.UseEndpointOrientedTopology();

                    c.Conventions().DefiningEventsAs(EventConvention);
                });
            }

            class TracerHandler : IHandleMessages<TracerMessage>
            {
                Context scenarioContext;

                public TracerHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(TracerMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.EventSubscribed = true;
                    return Task.CompletedTask;
                }
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.DisableFeature<AutoSubscribe>();
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    var ramp = routing.ConnectToRouter("Router");
                    ramp.RegisterPublisher(typeof(MyAsbEvent), PublisherEndpointName);
                    ramp.RouteToEndpoint(typeof(TracerMessage), PublisherEndpointName);

                    c.Conventions().DefiningEventsAs(EventConvention);
                });
            }

            class BaseEventHandler : IHandleMessages<MyAsbEvent>
            {
                Context scenarioContext;

                public BaseEventHandler(Context scenarioContext)
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

        class TracerMessage : IMessage
        {
        }
    }
}
#endif
