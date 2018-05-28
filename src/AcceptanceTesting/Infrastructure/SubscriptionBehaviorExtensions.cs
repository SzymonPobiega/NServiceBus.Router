using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

static class SubscriptionBehaviorExtensions
{
    public static void OnEndpointSubscribed<TContext>(this EndpointConfiguration configuration, Action<SubscriptionEventArgs, TContext> action) where TContext : ScenarioContext
    {
        configuration.Pipeline.Register("NotifySubscriptionBehavior", builder =>
        {
            var context = builder.Build<TContext>();
            return new SubscriptionBehavior<TContext>(action, context, MessageIntentEnum.Subscribe);
        }, "Provides notifications when endpoints subscribe");
    }

    public static void OnEndpointUnsubscribed<TContext>(this EndpointConfiguration configuration, Action<SubscriptionEventArgs, TContext> action) where TContext : ScenarioContext
    {
        configuration.Pipeline.Register("NotifyUnsubscriptionBehavior", builder =>
        {
            var context = builder.Build<TContext>();
            return new SubscriptionBehavior<TContext>(action, context, MessageIntentEnum.Unsubscribe);
        }, "Provides notifications when endpoints unsubscribe");
    }

    class SubscriptionBehavior<TContext> : IBehavior<ITransportReceiveContext, ITransportReceiveContext> where TContext : ScenarioContext
    {
        public SubscriptionBehavior(Action<SubscriptionEventArgs, TContext> action, TContext scenarioContext, MessageIntentEnum intentToHandle)
        {
            this.action = action;
            this.scenarioContext = scenarioContext;
            this.intentToHandle = intentToHandle;
        }

        public async Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            await next(context).ConfigureAwait(false);
            var subscriptionMessageType = GetSubscriptionMessageTypeFrom(context.Message);
            if (subscriptionMessageType != null)
            {
                if (!context.Message.Headers.TryGetValue(Headers.SubscriberTransportAddress, out var returnAddress))
                {
                    context.Message.Headers.TryGetValue(Headers.ReplyToAddress, out returnAddress);
                }

                var intent = (MessageIntentEnum)Enum.Parse(typeof(MessageIntentEnum), context.Message.Headers[Headers.MessageIntent], true);
                if (intent != intentToHandle)
                {
                    return;
                }

                action(new SubscriptionEventArgs
                {
                    MessageType = subscriptionMessageType,
                    SubscriberReturnAddress = returnAddress
                }, scenarioContext);
            }
        }

        static string GetSubscriptionMessageTypeFrom(IncomingMessage msg)
        {
            return msg.Headers.TryGetValue(Headers.SubscriptionMessageType, out var headerValue) ? headerValue : null;
        }

        Action<SubscriptionEventArgs, TContext> action;
        TContext scenarioContext;
        MessageIntentEnum intentToHandle;
    }
}

public class SubscriptionEventArgs
{
    /// <summary>
    /// The address of the subscriber.
    /// </summary>
    public string SubscriberReturnAddress { get; set; }

    /// <summary>
    /// The type of message the client subscribed to.
    /// </summary>
    public string MessageType { get; set; }
}