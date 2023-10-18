using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Features;

class ReportProcessingStatistics : FeatureStartupTask
{
    Statistics statistics;
    Task reportTask;
    CancellationTokenSource tokenSource;

    public ReportProcessingStatistics(Statistics statistics)
    {
        this.statistics = statistics;
    }

    protected override Task OnStart(IMessageSession session, CancellationToken token)
    {
        tokenSource = new CancellationTokenSource();
        reportTask = Task.Run(async () =>
        {
            while (!tokenSource.IsCancellationRequested)
            {
                await Task.Delay(2000, tokenSource.Token).ConfigureAwait(false);
                try
                {
                    var message = new ProcessingReport
                    {
                        MessagesProcessed = statistics.MessagesProcessed,
                    };
                    await session.Send(message).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        });
        return Task.CompletedTask;
    }

    protected override async Task OnStop(IMessageSession session, CancellationToken token)
    {
        tokenSource?.Cancel();
        if (reportTask != null)
        {
            try
            {
                await reportTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
        }
    }
}