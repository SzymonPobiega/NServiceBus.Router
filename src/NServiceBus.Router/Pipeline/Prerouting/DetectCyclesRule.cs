using System;
using System.Threading.Tasks;
using NServiceBus.Router;

class DetectCyclesRule : IRule<PreroutingContext, PreroutingContext>
{
    string routerName;

    public DetectCyclesRule(string routerName)
    {
        this.routerName = routerName;
    }

    public Task Invoke(PreroutingContext context, Func<PreroutingContext, Task> next)
    {
        if (context.Headers.TryGetValue("NServiceBus.Bridge.Trace", out var trace))
        {
            var cycleDetected = false;
            try
            {
                trace.DecodeTLV((t, v) =>
                {
                    if (t == "via" && v == routerName) //We forwarded this message
                    {
                        cycleDetected = true;
                    }
                });
            }
            catch (Exception ex)
            {
                throw new UnforwardableMessageException($"Cannot decode value in \'NServiceBus.Bridge.Trace\' header: {ex.Message}");
            }
            if (cycleDetected)
            {
                throw new UnforwardableMessageException($"Routing cycle detected: {trace}");
            }
        }

        return next(context);
    }
}
