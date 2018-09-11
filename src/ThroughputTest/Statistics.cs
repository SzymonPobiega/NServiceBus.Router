using System.Threading;

class Statistics
{
    long audits;

    public long Audits => Interlocked.Read(ref audits);

    public void AuditReceived()
    {
        Interlocked.Increment(ref audits);
    }
}