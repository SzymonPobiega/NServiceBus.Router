using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;

class AnycastToPostroutingConnector : IRule<AnycastContext, PostroutingContext>
{
    EndpointInstances endpointInstances;
    RawDistributionPolicy distributionPolicy;
    Func<EndpointInstance, string> resolveTransportAddress;

    public AnycastToPostroutingConnector(EndpointInstances endpointInstances, RawDistributionPolicy distributionPolicy, Func<EndpointInstance, string> resolveTransportAddress)
    {
        this.endpointInstances = endpointInstances;
        this.distributionPolicy = distributionPolicy;
        this.resolveTransportAddress = resolveTransportAddress;
    }

    public Task Invoke(AnycastContext context, Func<PostroutingContext, Task> next)
    {
        var candidates = endpointInstances.FindInstances(context.DestinationEndpoint)
            .Select(resolveTransportAddress)
            .ToArray();

        var selected = distributionPolicy.GetDistributionStrategy(context.DestinationEndpoint, context.DistributionScope).SelectDestination(candidates);

        var operation = new TransportOperation(context.Message, new UnicastAddressTag(selected));

        return next(new PostroutingContext(operation, context));
    }
}
