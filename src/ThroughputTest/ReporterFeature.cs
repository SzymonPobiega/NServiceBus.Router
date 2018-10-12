using Metrics;
using NServiceBus.Features;

public class ReporterFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        //var metricsContext = new DefaultMetricsContext();
        //var metricsConfig = new MetricsConfig(metricsContext);
        //metricsConfig.WithReporting(r =>
        //{
        //    r.WithCSVReports(".", TimeSpan.FromSeconds(5));
        //});

        //var processedMeter = metricsContext.Meter("Processed", Unit.Custom("audits"), TimeUnit.Seconds, new MetricTags());
        var processedMeter = Metric.Meter("Processed", Unit.Custom("audits"), TimeUnit.Seconds, new MetricTags());

        var statistics = new Statistics();

        context.Pipeline.Register(new StatisticsBehavior(statistics, processedMeter), "Captures processing statistics");
        context.RegisterStartupTask(new ReportProcessingStatistics(statistics));
    }
}