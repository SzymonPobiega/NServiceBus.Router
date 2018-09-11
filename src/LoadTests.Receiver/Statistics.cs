using System.Threading;

class Statistics
{
    long messages;

    public long MessagesProcessed => Interlocked.Read(ref messages);

    public void MessageReceived()
    {
        Interlocked.Increment(ref messages);
    }
}