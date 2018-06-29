using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

class SendForwarder
{
    EndpointInstances endpointInstances;
    RawDistributionPolicy distributionPolicy;

    public SendForwarder(EndpointInstances endpointInstances, RawDistributionPolicy distributionPolicy)
    {
        this.endpointInstances = endpointInstances;
        this.distributionPolicy = distributionPolicy;
    }

    public Task Forward(string incomingInterface, MessageContext context, IRawEndpoint dispatcher, RouteTable routeTable)
    {
        var destinations = context.Extensions.Get<Destination[]>();
        var routes = routeTable.Route(incomingInterface, destinations);

        TransportOperation Forward(Route r)
        {
            return r.Gateway != null 
                ? CreateTransportOperation(context, dispatcher, r.Gateway, incomingInterface, r.Destination)
                : CreateTransportOperation(context, dispatcher, r.Destination, incomingInterface, null);
        }

        var ops = routes.Select(Forward).ToArray();

        return dispatcher.Dispatch(new TransportOperations(ops), context.TransportTransaction, context.Extensions);
    }

    TransportOperation CreateTransportOperation(MessageContext context, IRawEndpoint dispatcher, string destinationEndpoint, string incomingInterface, string ultimateDestination)
    {
        var forwardedHeaders = new Dictionary<string, string>(context.Headers);

        var address = SelectDestinationAddress(destinationEndpoint, i => dispatcher.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));

        if (forwardedHeaders.TryGetValue(Headers.ReplyToAddress, out var replyToHeader)
            && forwardedHeaders.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            // pipe-separated TLV format
            var newCorrelationId = TLV.Encode("id", correlationId).AppendTLV("reply-to", replyToHeader);
            if (incomingInterface != null)
            {
                newCorrelationId = newCorrelationId.AppendTLV("iface", incomingInterface);
            }
            forwardedHeaders[Headers.CorrelationId] = newCorrelationId;
        }
        forwardedHeaders[Headers.ReplyToAddress] = dispatcher.TransportAddress;

        if (ultimateDestination != null)
        {
            forwardedHeaders["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
        }

        var outgoingMessage = new OutgoingMessage(context.MessageId, forwardedHeaders, context.Body);
        var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(address));
        return operation;
    }

    string SelectDestinationAddress(string endpoint, Func<EndpointInstance, string> resolveTransportAddress)
    {
        var candidates = endpointInstances.FindInstances(endpoint).Select(resolveTransportAddress).ToArray();
        var selected = distributionPolicy.GetDistributionStrategy(endpoint, DistributionStrategyScope.Send).SelectDestination(candidates);
        return selected;
    }
}
