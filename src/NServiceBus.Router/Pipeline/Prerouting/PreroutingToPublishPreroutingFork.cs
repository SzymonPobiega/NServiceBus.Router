using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class PreroutingToPublishPreroutingFork : ChainTerminator<PreroutingContext>
{
    protected override async Task<bool> Terminate(PreroutingContext context)
    {
        if (context.Intent == MessageIntentEnum.Publish)
        {
            if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
            {
                throw new UnforwardableMessageException("Message need to have 'NServiceBus.EnclosedMessageTypes' header in order to be routed.");
            }

            var types = messageTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            await context.Chains.Get<PublishPreroutingContext>()
                .Invoke(new PublishPreroutingContext(types, context))
                .ConfigureAwait(false);

            return true;
        }

        return false;
    }
}


