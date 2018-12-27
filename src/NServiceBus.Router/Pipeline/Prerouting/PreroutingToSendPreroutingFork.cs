using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class PreroutingToSendPreroutingFork : ChainTerminator<PreroutingContext>
{
    protected override async Task<bool> Terminate(PreroutingContext context)
    {
        if (context.Intent == MessageIntentEnum.Send)
        {
            await context.Chains.Get<SendPreroutingContext>()
                .Invoke(new SendPreroutingContext(context))
                .ConfigureAwait(false);

            return true;
        }

        return false;
    }
}


