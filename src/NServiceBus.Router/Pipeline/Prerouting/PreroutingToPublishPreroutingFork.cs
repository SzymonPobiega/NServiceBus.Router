using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class PreroutingToPublishPreroutingFork : IRule<PreroutingContext, PreroutingContext>
{
    public async Task Invoke(PreroutingContext context, Func<PreroutingContext, Task> next)
    {
        if (context.Intent == MessageIntentEnum.Publish)
        {
            if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
            {
                throw new UnforwardableMessageException("Message need to have 'NServiceBus.EnclosedMessageTypes' header in order to be routed.");
            }

            var types = messageTypes.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);

            await context.Chains.Get<PublishPreroutingContext>()
                .Invoke(new PublishPreroutingContext(types, context))
                .ConfigureAwait(false);
        }
        await next(context).ConfigureAwait(false);
    }
}


