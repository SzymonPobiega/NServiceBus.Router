using System;
using System.Threading.Tasks;
using Metrics;
using NServiceBus.Pipeline;

class StatisticsBehavior : Behavior<IIncomingLogicalMessageContext>
{
    readonly Statistics statistics;
    readonly Meter processedMeter;

    public StatisticsBehavior(Statistics statistics, Meter processedMeter)
    {
        this.statistics = statistics;
        this.processedMeter = processedMeter;
    }

    public override Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        statistics.MessageReceived();
        processedMeter.Mark();
        return Task.CompletedTask;
    }
}