using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class RuleCreationContext : IRuleCreationContext
{
    public EndpointInstances EndpointInstances { get; }
    public ISubscriptionStorage SubscriptionPersistence { get; }
    public RawDistributionPolicy DistributionPolicy { get; }
    public IRawEndpoint Endpoint { get; }

    public RuleCreationContext(EndpointInstances endpointInstances, ISubscriptionStorage subscriptionPersistence, RawDistributionPolicy distributionPolicy, IRawEndpoint endpoint)
    {
        EndpointInstances = endpointInstances;
        SubscriptionPersistence = subscriptionPersistence;
        DistributionPolicy = distributionPolicy;
        Endpoint = endpoint;
    }
}