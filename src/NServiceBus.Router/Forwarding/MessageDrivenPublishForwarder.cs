using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class MessageDrivenPublishForwarder : IPublishForwarder
{
    ISubscriptionStorage subscriptionStorage;
    RawDistributionPolicy distributionPolicy;

    public MessageDrivenPublishForwarder(ISubscriptionStorage subscriptionStorage, RawDistributionPolicy distributionPolicy)
    {
        this.subscriptionStorage = subscriptionStorage;
        this.distributionPolicy = distributionPolicy;
    }

    public async Task Forward(MessageContext context, IRawEndpoint dispatcher)
    {
        if (!context.Headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes))
        {
            throw new UnforwardableMessageException("Message need to have 'NServiceBus.EnclosedMessageTypes' header in order to be routed.");
        }
        var types = messageTypes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var typeObjects = types.Select(t => new MessageType(t));

        var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(typeObjects, new ContextBag()).ConfigureAwait(false);

        var destinations = SelectDestinationsForEachEndpoint(subscribers, context);
        var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
        var operations = destinations.Select(x => new TransportOperation(outgoingMessage, new UnicastAddressTag(x)));

        await dispatcher.Dispatch(new TransportOperations(operations.ToArray()), context.TransportTransaction, context.Extensions).ConfigureAwait(false);
    }

    IEnumerable<string> SelectDestinationsForEachEndpoint(IEnumerable<Subscriber> subscribers, MessageContext context)
    {
        //Make sure we are sending only one to each transport destination. Might happen when there are multiple routing information sources.
        var addresses = new HashSet<string>();
        Dictionary<string, List<string>> groups = null;
        foreach (var subscriber in subscribers)
        {
            if (subscriber.Endpoint == null)
            {
                addresses.Add(subscriber.TransportAddress);
                continue;
            }

            groups = groups ?? new Dictionary<string, List<string>>();

            if (groups.TryGetValue(subscriber.Endpoint, out var transportAddresses))
            {
                transportAddresses.Add(subscriber.TransportAddress);
            }
            else
            {
                groups[subscriber.Endpoint] = new List<string> { subscriber.TransportAddress };
            }
        }

        if (groups != null)
        {
            foreach (var group in groups)
            {
                var instances = group.Value.ToArray();
                var subscriber = distributionPolicy.GetDistributionStrategy(group.Key, DistributionStrategyScope.Publish).SelectDestination(instances);
                addresses.Add(subscriber);
            }
        }

        return addresses;
    }
}
