using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class ForwardPublishStorageDrivenRule : IRule<ForwardPublishContext, ForwardPublishContext>
{
    ISubscriptionStorage subscriptionStorage;
    RawDistributionPolicy distributionPolicy;

    public ForwardPublishStorageDrivenRule(ISubscriptionStorage subscriptionStorage, RawDistributionPolicy distributionPolicy)
    {
        this.subscriptionStorage = subscriptionStorage;
        this.distributionPolicy = distributionPolicy;
    }

    public async Task Invoke(ForwardPublishContext context, Func<ForwardPublishContext, Task> next)
    {
        var typeObjects = context.Types.Select(t => new MessageType(t));
        var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(typeObjects, new ContextBag()).ConfigureAwait(false);

        var destinations = SelectDestinationsForEachEndpoint(subscribers);
        var outgoingMessage = new OutgoingMessage(context.MessageId, context.ReceivedHeaders.Copy(), context.ReceivedBody);
        var operations = destinations.Select(x => new TransportOperation(outgoingMessage, new UnicastAddressTag(x)));

        var forkContext = new PostroutingContext(new TransportOperations(operations.ToArray()), context);
        var chain = context.Chains.Get<PostroutingContext>();
        await chain.Invoke(forkContext).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }

    IEnumerable<string> SelectDestinationsForEachEndpoint(IEnumerable<Subscriber> subscribers)
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