using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using AcceptanceTesting.Customization;
    using Features;
    using Routing;

    [TestFixture]
    public class When_router_is_scaled_out : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_use_both_instances_of_router()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router-A", (ctx, cfg) =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                    cfg.AddRule(_ => new DelegateRule(__ =>
                    {
                        ctx.MessageRoutedThroughA = true;
                        return Task.CompletedTask;
                    }));
                })
                .WithRouter("Router-B", (ctx, cfg) =>
                {
                    cfg.AddInterface<TestTransport>("Left", t => t.BrokerAlpha()).InMemorySubscriptions();
                    cfg.AddInterface<TestTransport>("Right", t => t.BrokerBravo()).InMemorySubscriptions();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                    cfg.AddRule(_ => new DelegateRule(__ =>
                    {
                        ctx.MessageRoutedThroughB = true;
                        return Task.CompletedTask;
                    }));
                })
                .WithEndpoint<Sender>(s => s.When(async (session, ctx) =>
                {
                    while (!ctx.MessageRoutedThroughA || !ctx.MessageRoutedThroughB)
                    {
                        await session.Send(new MyRequest()).ConfigureAwait(false);
                        await ctx.SendSemaphore.WaitAsync().ConfigureAwait(false);
                    }
                }))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageRoutedThroughA && c.MessageRoutedThroughB)
                .Run();

            Assert.IsTrue(result.MessageRoutedThroughA);
            Assert.IsTrue(result.MessageRoutedThroughB);
        }

        class DelegateRule : IRule<RawContext, RawContext>
        {
            Func<RawContext, Task> @delegate;

            public DelegateRule(Func<RawContext, Task> @delegate)
            {
                this.@delegate = @delegate;
            }

            public async Task Invoke(RawContext context, Func<RawContext, Task> next)
            {
                await @delegate(context);
                await next(context);
            }
        }

        class Context : ScenarioContext
        {
            public bool MessageRoutedThroughA { get; set; }
            public bool MessageRoutedThroughB { get; set; }
            public SemaphoreSlim SendSemaphore { get; } = new SemaphoreSlim(1);
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    var bridge = routing.ConnectToRouter("Router");
                    bridge.RouteToEndpoint(typeof(MyRequest), Conventions.EndpointNamingConvention(typeof(Receiver)));
                    c.EnableFeature<ScaleOutFeature>();
                });
            }

            class ScaleOutFeature : Feature
            {
                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.Settings.Get<EndpointInstances>().AddOrReplaceInstances("routers",
                        new List<EndpointInstance>
                        {
                            new EndpointInstance("Router", "A"),
                            new EndpointInstance("Router", "B"),
                        });
                }
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerBravo();
                });
            }

            class MyRequestHandler : IHandleMessages<MyRequest>
            {
                Context scenarioContext;

                public MyRequestHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyRequest request, IMessageHandlerContext context)
                {
                    scenarioContext.SendSemaphore.Release();
                    return Task.CompletedTask;
                }
            }
        }

        class MyRequest : IMessage
        {
        }
    }
}
