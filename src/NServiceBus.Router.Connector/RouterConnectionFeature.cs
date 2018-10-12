using System.Linq;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NServiceBus.Transport;

class RouterConnectionFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var transportInfra = context.Settings.Get<TransportInfrastructure>();
        var nativePubSub = transportInfra.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast;
        var settings = context.Settings.Get<RouterConnectionSettings>();
        var unicastRouteTable = context.Settings.Get<UnicastRoutingTable>();
        var route = UnicastRoute.CreateFromEndpointName(settings.RouterAddress);
        var publishers = context.Settings.Get<Publishers>();
        var bindings = context.Settings.Get<QueueBindings>();

        //Make sure router queue does exist.
        bindings.BindSending(settings.RouterAddress);

        //Send the specified messages through the router
        var routes = settings.SendRouteTable.Select(x => new RouteTableEntry(x.Key, route)).ToList();
        unicastRouteTable.AddOrReplaceRoutes("NServiceBus.Router", routes);

        var distributorAddress = context.Settings.GetOrDefault<string>("LegacyDistributor.Address");
        var subscriberAddress = distributorAddress ?? context.Settings.LocalAddress();

        var publisherAddress = PublisherAddress.CreateFromPhysicalAddresses(settings.RouterAddress);
        publishers.AddOrReplacePublishers("NServiceBus.Router", settings.PublisherTable.Select(kvp => new PublisherTableEntry(kvp.Key, publisherAddress)).ToList());

        context.Pipeline.Register(new ForwardSiteMessagesToRouterBehavior(settings.RouterAddress), "Routes messages sent to sites to the bridge.");
        context.Pipeline.Register(new RoutingHeadersBehavior(settings.SendRouteTable), "Sets the ultimate destination endpoint on the outgoing messages.");
        context.Pipeline.Register(b => new RouterSubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), settings.RouterAddress, b.Build<IDispatchMessages>(), settings.PublisherTable, nativePubSub), 
            "Dispatches the subscribe request via a router.");
        context.Pipeline.Register(b => new RouterUnsubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), settings.RouterAddress, b.Build<IDispatchMessages>(), settings.PublisherTable, nativePubSub),
            "Dispatches the unsubscribe request via a router.");
    }
}
