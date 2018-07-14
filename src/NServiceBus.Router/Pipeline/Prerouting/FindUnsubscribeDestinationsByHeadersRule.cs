using System;
using System.Threading.Tasks;
using NServiceBus.Router;

class FindUnsubscribeDestinationsByHeadersRule : FindDestinationsByHeadersRule,
    IRule<UnsubscribePreroutingContext, UnsubscribePreroutingContext>
{
    public Task Invoke(UnsubscribePreroutingContext context, Func<UnsubscribePreroutingContext, Task> next)
    {
        context.Destinations.AddRange(Find(context.Headers));
        return next(context);
    }
}