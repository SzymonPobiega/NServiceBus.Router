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

class ForwardPublishStorageDrivenRule : ChainTerminator<ForwardPublishContext>
{
    ISubscriptionStorage subscriptionStorage;
    RawDistributionPolicy distributionPolicy;

    public ForwardPublishStorageDrivenRule(ISubscriptionStorage subscriptionStorage, RawDistributionPolicy distributionPolicy)
    {
        this.subscriptionStorage = subscriptionStorage;
        this.distributionPolicy = distributionPolicy;
    }

    protected override async Task<bool> Terminate(ForwardPublishContext context)
    {
        var typeObjects = context.Types.Select(t => new MessageType(t));
        var subscribers = (await subscriptionStorage.GetSubscriberAddressesForMessage(typeObjects, new ContextBag()).ConfigureAwait(false)).ToArray();

        var tasks =
            CreateDispatchTasksForSubscribersWithoutEndpointName(context, subscribers)
                .Concat(CreateDispatchTasksForSubscribersWithEndpointNameAndAddress(context, subscribers))
                .Concat(CreateDispatchTasksForSubscribersWithEndpointNameOnly(context, subscribers))
                .ToArray();

        if (!tasks.Any())
        {
            return false;
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return true;

    }

    IEnumerable<Task> CreateDispatchTasksForSubscribersWithoutEndpointName(ForwardPublishContext context, Subscriber[] subscribers)
    {
        var operations = subscribers
            .Where(s => s.Endpoint == null)
            .Select(x => new TransportOperation(new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody), new UnicastAddressTag(x.TransportAddress)));

        var contexts = operations.Select(o => new PostroutingContext(o, context));
        var chain = context.Chains.Get<PostroutingContext>();

        var tasks = contexts.Select(c => chain.Invoke(c));
        return tasks;
    }

    IEnumerable<Task> CreateDispatchTasksForSubscribersWithEndpointNameAndAddress(ForwardPublishContext context, Subscriber[] subscribers)
    {
        var matchingSubscribers = subscribers.Where(s => s.Endpoint != null && s.TransportAddress != null);
        var destinations = SelectDestinationsForEachEndpoint(matchingSubscribers);

        var operations = destinations
            .Select(x => new TransportOperation(new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody), new UnicastAddressTag(x)));

        var contexts = operations.Select(o => new PostroutingContext(o, context));
        var chain = context.Chains.Get<PostroutingContext>();

        var tasks = contexts.Select(c => chain.Invoke(c));
        return tasks;
    }

    IEnumerable<Task> CreateDispatchTasksForSubscribersWithEndpointNameOnly(ForwardPublishContext context, Subscriber[] subscribers)
    {
        var matchingSubscribers = subscribers.Where(s => s.Endpoint != null && s.TransportAddress == null);
        var contexts = matchingSubscribers.Select(s =>
        {
            var message = new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody);
            return new AnycastContext(s.Endpoint, message, DistributionStrategyScope.Publish, context);
        });
        var chain = context.Chains.Get<AnycastContext>();

        var tasks = contexts.Select(c => chain.Invoke(c));
        return tasks;
    }

    IEnumerable<string> SelectDestinationsForEachEndpoint(IEnumerable<Subscriber> subscribers)
    {
        //Make sure we are sending only one to each transport destination. Might happen when there are multiple routing information sources.
        var addresses = new HashSet<string>();
        Dictionary<string, List<string>> groups = null;
        foreach (var subscriber in subscribers)
        {
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