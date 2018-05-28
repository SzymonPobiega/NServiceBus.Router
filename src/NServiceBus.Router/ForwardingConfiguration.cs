using System;
using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class ForwardingConfiguration
{
    RuntimeTypeGenerator typeGenerator;
    EndpointInstances endpointInstances;
    ISubscriptionStorage subscriptionPersistence;
    RawDistributionPolicy distributionPolicy;

    public ForwardingConfiguration(RuntimeTypeGenerator typeGenerator, EndpointInstances endpointInstances, ISubscriptionStorage subscriptionPersistence, RawDistributionPolicy distributionPolicy)
    {
        this.typeGenerator = typeGenerator;
        this.endpointInstances = endpointInstances;
        this.subscriptionPersistence = subscriptionPersistence;
        this.distributionPolicy = distributionPolicy;
    }

    public void PreparePubSub(IRawEndpoint endpoint, out IPublishForwarder publishForwarder, out SubscriptionReceiver subscriptionReceiver, out SubscriptionForwarder subscriptionForwarder)
    {
        var transport = endpoint.Settings.Get<TransportInfrastructure>();
        if (transport.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast)
        {
            publishForwarder = new NativePublishForwarder(typeGenerator);
            subscriptionReceiver = new NullSubscriptionReceiver();
            subscriptionForwarder = new NativeSubscriptionForwarder(endpoint.SubscriptionManager, typeGenerator, endpointInstances);
        }
        else
        {
            if (subscriptionPersistence == null)
            {
                throw new Exception("Subscription storage has not been configured. Use 'UseSubscriptionPersistence' method to configure it.");
            }
            publishForwarder = new MessageDrivenPublishForwarder(subscriptionPersistence, distributionPolicy);
            subscriptionReceiver = new StorageDrivenSubscriptionReceiver(subscriptionPersistence);
            subscriptionForwarder = new MessageDrivenSubscriptionForwarder(endpointInstances);
        }
    }

    public SendForwarder PrepareSending()
    {
        return new SendForwarder(endpointInstances, distributionPolicy);
    }
}