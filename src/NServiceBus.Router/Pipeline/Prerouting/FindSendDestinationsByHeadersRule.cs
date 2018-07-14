using System;
using System.Threading.Tasks;
using NServiceBus.Router;

class FindSendDestinationsByHeadersRule : FindDestinationsByHeadersRule,
    IRule<SendPreroutingContext, SendPreroutingContext>
{
    public Task Invoke(SendPreroutingContext context, Func<SendPreroutingContext, Task> next)
    {
        context.Destinations.AddRange(Find(context.Headers));
        return next(context);
    }
}