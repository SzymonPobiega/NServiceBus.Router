﻿using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System.Collections.Generic;
    using Routing;

    [TestFixture]
    public class When_publisher_is_scaled_out : NServiceBusAcceptanceTest
    {
        static readonly string PublisherEndpointName = "PublisherIsScaledOut.Publisher";

        [Test]
        public async Task Should_deliver_subscribe_messages_to_all_instances()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    var leftIface = cfg.AddInterface("Left", false);
                    leftIface.Broker().Alpha();
                    cfg.AddInterface("Right", false).Broker().Bravo();
                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Right", "Left");

                    leftIface.EndpointInstances.AddOrReplaceInstances("publishers", new List<EndpointInstance>
                    {
                        new EndpointInstance(PublisherEndpointName, "A"),
                        new EndpointInstance(PublisherEndpointName, "B"),
                    });

                })
                .WithEndpoint<PublisherA>()
                .WithEndpoint<PublisherB>()
                .WithEndpoint<Subscriber>()
                .Done(c => c.SubscribeReceivedByA && c.SubscribeReceivedByB)
                .Run();

            Assert.IsTrue(result.SubscribeReceivedByA);
            Assert.IsTrue(result.SubscribeReceivedByB);
        }

        class Context : ScenarioContext
        {
            public bool SubscribeReceivedByA { get; set; }
            public bool SubscribeReceivedByB { get; set; }
        }

        class PublisherA : EndpointConfigurationBuilder
        {
            public PublisherA()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();
                    c.MakeInstanceUniquelyAddressable("A");
                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.SubscribeReceivedByA = true;
                    });
                }).CustomEndpointName(PublisherEndpointName);
            }
        }

        class PublisherB : EndpointConfigurationBuilder
        {
            public PublisherB()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();
                    c.MakeInstanceUniquelyAddressable("B");
                    c.OnEndpointSubscribed<Context>((args, context) =>
                    {
                        context.SubscribeReceivedByB = true;
                    });
                }).CustomEndpointName(PublisherEndpointName);
            }
        }

        class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Bravo();

                    var routing = c.ConfigureRouting();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RegisterPublisher(typeof(MyEvent), PublisherEndpointName);
                });
            }

            class MyRequestHandler : IHandleMessages<MyEvent>
            {
                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    return Task.CompletedTask;
                }
            }
        }

        class MyEvent : IEvent
        {
        }
    }
}
