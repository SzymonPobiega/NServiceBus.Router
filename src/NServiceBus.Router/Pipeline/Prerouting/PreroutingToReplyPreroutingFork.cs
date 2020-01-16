using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class PreroutingToReplyPreroutingFork : IRule<PreroutingContext, PreroutingContext>
{
    public async Task Invoke(PreroutingContext context, Func<PreroutingContext, Task> next)
    {
        if (context.Intent == MessageIntentEnum.Reply)
        {
            await context.Chains.Get<ReplyPreroutingContext>()
                .Invoke(new ReplyPreroutingContext(context))
                .ConfigureAwait(false);

            context.MarkForwarded();
        }

        await next(context).ConfigureAwait(false);
    }
}


