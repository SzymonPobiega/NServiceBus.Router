using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;

    [TestFixture]
    public class When_intentionally_dropping_messages : NServiceBusAcceptanceTest
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
                    cfg.AddRule(_ => new CustomDestinationRule(ctx));
                })
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new MyRequest())))
                .Done(c => c.Dropped)
                .Run(TimeSpan.FromSeconds(20));

            Assert.IsTrue(result.Dropped);
        }

        class CustomDestinationRule : IRule<SendPreroutingContext, SendPreroutingContext>
        {
            Context scenarioContext;

            public CustomDestinationRule(Context scenarioContext)
            {
                this.scenarioContext = scenarioContext;
            }

            public Task Invoke(SendPreroutingContext context, Func<SendPreroutingContext, Task> next)
            {
                if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var types) 
                    && types.Contains("MyRequest"))
                {
                    scenarioContext.Dropped = true;
                    return Task.CompletedTask;
                }

                return next(context);
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
                });
            }
        }

        class MyRequest : IMessage
        {
        }
    }
}
