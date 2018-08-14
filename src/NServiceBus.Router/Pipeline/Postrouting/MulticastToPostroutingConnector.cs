using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;

class MulticastToPostroutingConnector : IRule<MulticastContext, PostroutingContext>
{
    EndpointInstances endpointInstances;
    Func<EndpointInstance, string> resolveTransportAddress;

    public MulticastToPostroutingConnector(EndpointInstances endpointInstances, Func<EndpointInstance, string> resolveTransportAddress)
    {
        this.endpointInstances = endpointInstances;
        this.resolveTransportAddress = resolveTransportAddress;
    }

    public Task Invoke(MulticastContext context, Func<PostroutingContext, Task> next)
    {
        var addresses = endpointInstances.FindInstances(context.DestinationEndpoint)
            .Select(resolveTransportAddress)
            .ToArray();

        var operations = addresses.Select(a => new TransportOperation(context.Message, new UnicastAddressTag(a)));

        return next(new PostroutingContext(operations.ToArray(), context));
    }
}
