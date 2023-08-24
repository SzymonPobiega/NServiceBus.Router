using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class MessageDrivenPubSubFeature : IFeature
{
    const string SettingsKey = "EnableMessageDrivenPubSub";

    public void Configure(RouterConfiguration routerConfig)
    {
        routerConfig.AddRule(c => new ForwardPublishStorageDrivenRule(GetSubscriptionStorage(c), c.DistributionPolicy), c => IsEnabled(c));
        routerConfig.AddRule(c => new ForwardPublishNullRule(), c => IsExplicitlyDisabled(c));
        routerConfig.AddRule(c => new ForwardSubscribeMessageDrivenRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName), c => IsEnabled(c) && !SendOnly(c));
        routerConfig.AddRule(c => new ForwardUnsubscribeMessageDrivenRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName), c => IsEnabled(c) && !SendOnly(c));
        routerConfig.AddRule(c => new StorageDrivenSubscriptionRule(GetSubscriptionStorage(c)), c => IsEnabled(c) && !SendOnly(c));
    }

    static ISubscriptionStorage GetSubscriptionStorage(IRuleCreationContext c)
    {
        if (!c.Settings.TryGet<ISubscriptionStorage>(out var subscriptionStorage))
        {
            throw new Exception($"Interface {c.InterfaceName} does not support native pub/sub and message-driven pub/sub has not been configured. "
                                +"Either explicitly disable or enable the message-driven pub/sub for this interface. "
                                +"If message-driven pub/sub is disabled the interface won't be able to forward published messages.");
        }

        return subscriptionStorage;
    }

    static bool IsEnabled(IRuleCreationContext context)
    {
        if (context.Settings.HasExplicitValue(SettingsKey))
        {
            return context.Settings.Get<bool>(SettingsKey);
        }
        return context.Endpoint.SubscriptionManager == null;
    }

    static bool IsExplicitlyDisabled(IRuleCreationContext context)
    {
        return context.Settings.HasExplicitValue(SettingsKey) 
               && false == context.Settings.Get<bool>(SettingsKey);
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