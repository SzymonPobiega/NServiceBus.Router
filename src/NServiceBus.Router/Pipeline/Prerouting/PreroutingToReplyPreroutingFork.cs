using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class PreroutingToReplyPreroutingFork : ChainTerminator<PreroutingContext>
{
    protected override async Task<bool> Terminate(PreroutingContext context)
    {
        if (context.Intent == MessageIntentEnum.Reply)
        {
            await context.Chains.Get<ReplyPreroutingContext>()
                .Invoke(new ReplyPreroutingContext(context))
                .ConfigureAwait(false);

            return true;
        }

        return false;
    }
}


