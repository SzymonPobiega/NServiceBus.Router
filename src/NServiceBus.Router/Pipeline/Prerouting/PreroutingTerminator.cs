using System.Threading.Tasks;
using NServiceBus.Router;

class PreroutingTerminator : ChainTerminator<PreroutingContext>
{
    protected override Task<bool> Terminate(PreroutingContext context)
    {
        if (!context.Dropped && !context.Forwarded)
        {
            throw new UnforwardableMessageException($"The incoming message {context.MessageId} has not been forwarded. This might indicate a configuration problem.");
        }
        return Task.FromResult(true);
    }
}