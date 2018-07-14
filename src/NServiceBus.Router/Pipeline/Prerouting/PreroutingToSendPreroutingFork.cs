using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class PreroutingToSendPreroutingFork : IRule<PreroutingContext, PreroutingContext>
{
    public async Task Invoke(PreroutingContext context, Func<PreroutingContext, Task> next)
    {
        if (context.Intent == MessageIntentEnum.Send)
        {
            await context.Chains.Get<SendPreroutingContext>()
                .Invoke(new SendPreroutingContext(context))
                .ConfigureAwait(false);

        }
        await next(context).ConfigureAwait(false);
    }
}


