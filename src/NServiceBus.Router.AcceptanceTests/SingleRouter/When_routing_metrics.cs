using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
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
            var spyTransport = new AcceptanceTestingTransport(false, false);
            spyTransport.Broker().Bravo();

            var result = await Scenario.Define<Context>()
                .WithRouter("Router", cfg =>
                {
                    var msmq = cfg.AddInterface("MSMQ");
                    msmq.DisableMessageDrivenPublishSubscribe();
                    msmq.Broker().Alpha();
                    var sql = cfg.AddInterface("SQL"); 
                    sql.DisableMessageDrivenPublishSubscribe();
                    sql.Broker().Bravo();

                    cfg.UseStaticRoutingProtocol();

                    cfg.AddRule(c => new MetricsPreroutingTerminator("SQL", "Metrics"));
                })
                .WithSpyComponent("Metrics",  spyTransport, (scenarioContext, messageContext, _, __) =>
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
                if (context.Headers.ContainsKey("NServiceBus.Metric.Type") || 
                    context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes) && messageTypes == "NServiceBus.Metrics.EndpointMetadataReport")
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
                    c.ConfigureBroker().Alpha();
                    c.EnableMetrics().SendMetricDataToServiceControl("Router", TimeSpan.FromSeconds(1));
                });
            }
        }
    }
}
