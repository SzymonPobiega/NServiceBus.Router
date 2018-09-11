using System.Threading.Tasks;
using NServiceBus;

class ProcessingReportHandler : IHandleMessages<ProcessingReport>
{
    public ProcessingReportHandler(LoadGenerator generator)
    {
        this.generator = generator;
    }

    public Task Handle(ProcessingReport message, IMessageHandlerContext context)
    {
        return generator.ProcessedCountReported(message.Audits);
    }

    LoadGenerator generator;
}