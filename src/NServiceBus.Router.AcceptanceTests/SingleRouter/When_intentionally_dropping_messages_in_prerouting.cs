using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using Pipeline;

    [TestFixture]
    public class When_intentionally_dropping_messages_in_prerouting : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_not_complain()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", (ctx, cfg) =>
                {
                    cfg.AddInterface("Left", false).Broker().Alpha();
                    cfg.AddInterface("Right", false).Broker().Bravo();

                    cfg.UseStaticRoutingProtocol().AddForwardRoute("Left", "Right");
                    cfg.AddRule(_ => new DropMessagesRule(ctx));
                })
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
                .Done(c => c.Dropped)
                .Run(TimeSpan.FromSeconds(20));

            Assert.IsTrue(result.Dropped);
        }

        class DropMessagesRule : IRule<PreroutingContext, PreroutingContext>
        {
            Context scenarioContext;

            public DropMessagesRule(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public async Task Invoke(PreroutingContext context, Func<PreroutingContext, Task> next)
            {
                context.DoNotRequireThisMessageToBeForwarded();
                await next(context);
                scenarioContext.Dropped = true;
            }
        }

        class Context : ScenarioContext
        {
            public bool Dropped { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.ConfigureBroker().Alpha();

                    var routing = c.ConfigureRouting();
                    var router = routing.ConnectToRouter("Router");
                    router.DelegateRouting(typeof(MyRequest));
                    c.Pipeline.Register(new RemoveIntentBehavior(), "Remove message intent header");
                });
            }

            class RemoveIntentBehavior : Behavior<IDispatchContext>
            {
                public override Task Invoke(IDispatchContext context, Func<Task> next)
                {
                    foreach (var operation in context.Operations)
                    {
                        operation.Message.Headers.Remove(Headers.MessageIntent);
                    }

                    return next();
                }
            }
        }

        class MyRequest : IMessage
        {
        }
    }
}
