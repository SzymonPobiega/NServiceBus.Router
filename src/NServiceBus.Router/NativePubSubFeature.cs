using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class NativePubSubFeature : IFeature
{
    public void Configure(RouterConfiguration routerConfig)
    {
        routerConfig.AddRule(c => new ForwardPublishNativeRule(), c => EnableNativePubSub(c));
        routerConfig.AddRule(c => new ForwardPublishNullRule(), c => c.Settings.HasExplicitValue("NativePubSubDisabled"));
        routerConfig.AddRule(c => new ForwardSubscribeNativeRule(c.Endpoint.SubscriptionManager), c => EnableNativePubSub(c) && !SendOnly(c));
        routerConfig.AddRule(c => new ForwardUnsubscribeNativeRule(c.Endpoint.SubscriptionManager), c => EnableNativePubSub(c) && !SendOnly(c));
    }

    static bool EnableNativePubSub(IRuleCreationContext context)
    {
        if (context.Settings.GetOrDefault<bool>("NativePubSubDisabled"))
        {
            return false;
        }
        return context.Endpoint.SubscriptionManager != null;
    }

    static bool SendOnly(IRuleCreationContext context)
    {
        return context.Endpoint.TransportAddress == null;
    }

    class ForwardPublishNullRule : ChainTerminator<ForwardPublishContext>
    {
        static Task<bool> falseResult = Task.FromResult(false);

        protected override Task<bool> Terminate(ForwardPublishContext context)
        {
            return falseResult;
        }
    }
}