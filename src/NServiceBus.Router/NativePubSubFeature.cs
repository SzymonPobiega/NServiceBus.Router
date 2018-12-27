using NServiceBus.Router;
using NServiceBus.Transport;

class NativePubSubFeature : IFeature
{
    public void Configure(RouterConfiguration routerConfig)
    {
        routerConfig.AddRule(c => new ForwardPublishNativeRule(), c => EnableNativePubSub(c));
        routerConfig.AddRule(c => new ForwardSubscribeNativeRule(c.Endpoint.SubscriptionManager), c => EnableNativePubSub(c));
        routerConfig.AddRule(c => new ForwardUnsubscribeNativeRule(c.Endpoint.SubscriptionManager), c => EnableNativePubSub(c));
    }

    static bool EnableNativePubSub(IRuleCreationContext context)
    {
        if (context.Settings.GetOrDefault<bool>("NativePubSubDisabled"))
        {
            return false;
        }
        var transport = context.Endpoint.Settings.Get<TransportInfrastructure>();
        return transport.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast;
    }
}