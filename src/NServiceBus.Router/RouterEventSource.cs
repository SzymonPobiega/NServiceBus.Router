using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

[EventSource(Name = RouterEventSource.SourceName)]
class RouterEventSource : EventSource
{
    // this name will be used as "provider" name with dotnet-counters
    // ex: dotnet-counters monitor -p <pid> Sample.RequestCounters
    //
    const string SourceName = "NServiceBus.Router";

    IncrementingEventCounter messagesProcessedTotal;
    ConcurrentDictionary<string, EventCounter> processingTimeByInterface = new ConcurrentDictionary<string, EventCounter>();
    ConcurrentDictionary<string, IncrementingEventCounter> messagesProcessedByInterface = new ConcurrentDictionary<string, IncrementingEventCounter>();
    ConcurrentDictionary<string, IncrementingEventCounter> retriesByInterface = new ConcurrentDictionary<string, IncrementingEventCounter>();
    ConcurrentDictionary<string, IncrementingEventCounter> poisonByInterface = new ConcurrentDictionary<string, IncrementingEventCounter>();

    public static RouterEventSource Instance = new RouterEventSource();

    public RouterEventSource()
        : base(RouterEventSource.SourceName, EventSourceSettings.EtwSelfDescribingEventFormat)
    {
        messagesProcessedTotal = new IncrementingEventCounter("Messages processed", this);
    }

    public void MessageProcessed(string routerName, string interfaceName, float elapsedMilliseconds)
    {
        var messagesProcessed = messagesProcessedByInterface.GetOrAdd(interfaceName, _ => new IncrementingEventCounter($"{routerName}.{interfaceName} - messages processed", this));
        messagesProcessed.Increment();
        messagesProcessedTotal.Increment();

        var processingTime = processingTimeByInterface.GetOrAdd(interfaceName, _ => new EventCounter($"{routerName}.{interfaceName} - processing time", this));
        processingTime.WriteMetric(elapsedMilliseconds);

    }

    public void MessageFailed(string routerName, string interfaceName)
    {
        var counter = retriesByInterface.GetOrAdd(interfaceName, _ => new IncrementingEventCounter($"{routerName}.{interfaceName} - retries", this));
        counter.Increment();
    }

    public void MessageMovedToPoisonQueue(string routerName, string interfaceName)
    {
        var counter = poisonByInterface.GetOrAdd(interfaceName, _ => new IncrementingEventCounter($"{routerName}.{interfaceName} - poison messages", this));
        counter.Increment();
    }

}