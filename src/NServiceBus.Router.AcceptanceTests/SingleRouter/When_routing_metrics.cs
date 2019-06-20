using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using Transport;

    [TestFixture]
    public class When_routing_metrics : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_deliver_the_reply_without_the_need_to_configure_the_bridge()
        {
            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    cfg.AddInterface<TestTransport>("MSMQ", t => t.BrokerAlpha()).DisableMessageDrivenPublishSubscribe();
                    cfg.AddInterface<TestTransport>("SQL", t => t.BrokerBravo()).DisableMessageDrivenPublishSubscribe();
                    cfg.UseStaticRoutingProtocol();

                    cfg.Chains.AddRule(c => new MetricsPreroutingTerminator("SQL", "Metrics"));
                })
                .WithSpyComponent("Metrics", t => t.BrokerBravo(), (scenarioContext, messageContext, _) =>
                {
                    if (messageContext.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                        && messageTypes == "NServiceBus.Metrics.EndpointMetadataReport")
                    {
                        scenarioContext.MetricReceived = true;
                    }

                    return Task.CompletedTask;
                })
                .WithEndpoint<Sender>()
                .Done(c => c.MetricReceived)
                .Run();


            Assert.IsTrue(result.MetricReceived);
        }

        class MetricsPreroutingTerminator : ChainTerminator<PreroutingContext>
        {
            string metricsEndpoint;
            string metricsInterface;

            public MetricsPreroutingTerminator(string metricsInterface, string metricsEndpoint)
            {
                this.metricsEndpoint = metricsEndpoint;
                this.metricsInterface = metricsInterface;
            }

            protected override async Task<bool> Terminate(PreroutingContext context)
            {
                if (context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                    && messageTypes == "NServiceBus.Metrics.EndpointMetadataReport")
                {
                    var interfaces = context.Extensions.Get<IInterfaceChains>();
                    await interfaces.GetChainsFor(metricsInterface).Get<AnycastContext>()
                        .Invoke(new AnycastContext(metricsEndpoint, new OutgoingMessage(context.MessageId, context.Headers.Copy(), context.Body), DistributionStrategyScope.Send,  context))
                        .ConfigureAwait(false);

                    return true;
                }

                return false;
            }
        }

        class Context : ScenarioContext
        {
            public bool MetricReceived { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerAlpha();
                    c.EnableMetrics().SendMetricDataToServiceControl("Router", TimeSpan.FromSeconds(1));
                });
            }
        }
    }
}
