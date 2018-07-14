using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class PreroutingToSubscribePreroutingFork : IRule<PreroutingContext, PreroutingContext>
{
    public async Task Invoke(PreroutingContext context, Func<PreroutingContext, Task> next)
    {
        if (context.Intent == MessageIntentEnum.Subscribe
            || context.Intent == MessageIntentEnum.Unsubscribe)
        {
            var messageTypeString = GetSubscriptionMessageTypeFrom(context);

            if (string.IsNullOrEmpty(messageTypeString))
            {
                throw new UnforwardableMessageException("The subscription message type header is missing.");
            }

            string subscriberEndpoint = null;

            if (context.Headers.TryGetValue(Headers.SubscriberTransportAddress, out var subscriberAddress))
            {
                subscriberEndpoint = context.Headers[Headers.SubscriberEndpoint];
            }
            else
            {
                subscriberAddress = GetReplyToAddress(context);
            }

            if (subscriberAddress == null)
            {
                throw new UnforwardableMessageException("Subscription message arrived without a valid ReplyToAddress.");
            }

            if (context.Intent == MessageIntentEnum.Subscribe)
            {
                await context.Chains.Get<SubscribePreroutingContext>()
                    .Invoke(new SubscribePreroutingContext(context, messageTypeString, subscriberEndpoint, subscriberAddress))
                    .ConfigureAwait(false);
            }
            else
            {
                await context.Chains.Get<UnsubscribePreroutingContext>()
                    .Invoke(new UnsubscribePreroutingContext(context, messageTypeString, subscriberEndpoint, subscriberAddress))
                    .ConfigureAwait(false);
            }
        }
        await next(context).ConfigureAwait(false);
    }

    static string GetReplyToAddress(PreroutingContext message)
    {
        return message.Headers.TryGetValue(Headers.ReplyToAddress, out var replyToAddress) ? replyToAddress : null;
    }

    static string GetSubscriptionMessageTypeFrom(PreroutingContext msg)
    {
        msg.Headers.TryGetValue(Headers.SubscriptionMessageType, out var value);
        return value;
    }
}


