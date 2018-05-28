using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;

class RepeatedFailuresCircuitBreaker
{
    public RepeatedFailuresCircuitBreaker(string name, long threshold, Action<Exception> triggerAction)
    {
        this.name = name;
        this.threshold = threshold;
        this.triggerAction = triggerAction;
    }

    public void Success()
    {
        var oldValue = Interlocked.Exchange(ref failureCount, 0);

        if (oldValue == 0)
        {
            return;
        }

        Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
    }

    public bool IsArmed => failureCount > 0;

    public Task Failure(Exception exception)
    {
        var newValue = Interlocked.Increment(ref failureCount);

        if (newValue == 1)
        {
            Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
        }

        if (newValue >= threshold)
        {
            Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);
            triggerAction(exception);
            failureCount = 0;
            return Task.FromResult(0);
        }
        return Task.Delay(TimeSpan.FromSeconds(1));
    }

    string name;
    long threshold;
    Action<Exception> triggerAction;
    long failureCount;

    static ILog Logger = LogManager.GetLogger<RepeatedFailuresCircuitBreaker>();
}