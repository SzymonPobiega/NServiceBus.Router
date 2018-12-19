using System;
using System.Threading.Tasks;
using NServiceBus.Router;

class RawToPreroutingConnector : IRule<RawContext, PreroutingContext>
{
    public Task Invoke(RawContext context, Func<PreroutingContext, Task> next)
    {
        return next(new PreroutingContext(context));
    }
}


