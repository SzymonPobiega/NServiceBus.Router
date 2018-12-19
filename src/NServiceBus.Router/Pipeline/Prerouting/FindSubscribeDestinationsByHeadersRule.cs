using System;
using System.Threading.Tasks;
using NServiceBus.Router;

class FindSubscribeDestinationsByHeadersRule : FindDestinationsByHeadersRule, 
    IRule<SubscribePreroutingContext, SubscribePreroutingContext>
{
    public Task Invoke(SubscribePreroutingContext context, Func<SubscribePreroutingContext, Task> next)
    {
        context.Destinations.AddRange(Find(context.Headers));
        return next(context);
    }
}