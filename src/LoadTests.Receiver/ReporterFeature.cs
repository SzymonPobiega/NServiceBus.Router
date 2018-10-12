using Metrics;
using NServiceBus.Features;

public class ReporterFeature : Feature
{
    public ReporterFeature()
    {
        EnableByDefault();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var processedMeter = Metric.Meter("Received", Unit.Custom("messages"));

        var statistics = new Statistics();

        context.Pipeline.Register(new StatisticsBehavior(statistics, processedMeter), "Captures processing statistics");
        context.RegisterStartupTask(new ReportProcessingStatistics(statistics));
    }
}