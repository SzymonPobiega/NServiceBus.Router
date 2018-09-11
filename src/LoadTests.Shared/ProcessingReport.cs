using NServiceBus;

[TimeToBeReceived("00:00:10")]
public class ProcessingReport : IMessage
{
    public long MessagesProcessed { get; set; }
}