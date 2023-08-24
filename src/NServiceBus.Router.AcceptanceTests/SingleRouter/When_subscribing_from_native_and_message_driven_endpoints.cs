﻿using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using AcceptanceTesting.Customization;

    [TestFixture]
    public class When_subscribing_from_native_and_message_driven_endpoints : NServiceBusAcceptanceTest
    {
        static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task It_should_deliver_the_message_to_both_subscribers()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    var b = cfg.AddInterface("B");
                    b.LimitMessageProcessingConcurrencyTo(1);
                    b.Broker().Yankee();

                    //BaseEventSubscriber - Broker A
                    var a = cfg.AddInterface("A", false);
                    a.Broker().Alpha();

                    //DerivedEventSubscriber - Broker C`
                    cfg.AddInterface("C").Broker().Zulu();

                    var routeTable = cfg.UseStaticRoutingProtocol();
                    routeTable.AddForwardRoute("A", "B");
                    routeTable.AddForwardRoute("C", "B");
                })
                .WithEndpoint<Publisher>(c => c.When(x => x.BaseEventSubscribed && x.DerivedEventSubscribed, s => s.Publish(new MyDerivedEvent3())))
                .WithEndpoint<BaseEventSubscriber>(c => c.When(async s =>
                {
                    await s.Subscribe<MyBaseEvent3>().ConfigureAwait(false);
                    await s.Send(new TracerMessage()).ConfigureAwait(false);
                }))
                .WithEndpoint<DerivedEventSubscriber>(c => c.When(async s =>
                {
                    await s.Subscribe<MyDerivedEvent3>().ConfigureAwait(false);
                    await s.Send(new TracerMessage()).ConfigureAwait(false);
                }))
                .Done(c => c.BaseEventDelivered && c.DerivedEventDeilvered)
                .Run();

            Assert.IsTrue(result.BaseEventDelivered);
            Assert.IsTrue(result.DerivedEventDeilvered);
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
                    c.ConfigureBroker().Yankee();
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
                    if (context.MessageHeaders[Headers.OriginatingEndpoint].Contains("BaseEventSubscriber"))
                    {
                        scenarioContext.BaseEventSubscribed = true;
                    }
                    else
                    {
                        scenarioContext.DerivedEventSubscribed = true;
                    }
                    return Task.CompletedTask;
                }
            }
        }

        class BaseEventSubscriber : EndpointConfigurationBuilder
        {
            public BaseEventSubscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();
                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyBaseEvent3), PublisherEndpoint);
                    bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpoint);
                });
            }

            class BaseEventHandler : IHandleMessages<MyBaseEvent3>
            {
                Context scenarioContext;

                public BaseEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyBaseEvent3 message, IMessageHandlerContext context)
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
                    c.ConfigureBroker().Zulu();

                    var routing = c.ConfigureRouting();

                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyDerivedEvent3), PublisherEndpoint);
                    bridge.RouteToEndpoint(typeof(TracerMessage), PublisherEndpoint);
                });
            }

            class DerivedEventHandler : IHandleMessages<MyDerivedEvent3>
            {
                Context scenarioContext;

                public DerivedEventHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyDerivedEvent3 message, IMessageHandlerContext context)
                {
                    scenarioContext.DerivedEventDeilvered = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MyBaseEvent3 : IEvent
        {
        }

        class MyDerivedEvent3 : MyBaseEvent3
        {
        }

        class TracerMessage : IMessage
        {
        }
    }
}
