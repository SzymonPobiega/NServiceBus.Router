using System.Linq;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Routing.MessageDrivenSubscriptions;
using NServiceBus.Transport;

class BridgeRoutingFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var transportInfra = context.Settings.Get<TransportInfrastructure>();
        var nativePubSub = transportInfra.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast;
        var settings = context.Settings.Get<BridgeRoutingSettings>();
        var unicastRouteTable = context.Settings.Get<UnicastRoutingTable>();
        var route = UnicastRoute.CreateFromPhysicalAddress(settings.BridgeAddress);
        var publishers = context.Settings.Get<Publishers>();
        var bindings = context.Settings.Get<QueueBindings>();

        //Make sure bridge queue does exist.
        bindings.BindSending(settings.BridgeAddress);

        //Send the specified messages through the bridge
        var routes = settings.SendRouteTable.Select(x => new RouteTableEntry(x.Key, route)).ToList();
        unicastRouteTable.AddOrReplaceRoutes("NServiceBus.Bridge", routes);

        var distributorAddress = context.Settings.GetOrDefault<string>("LegacyDistributor.Address");
        var subscriberAddress = distributorAddress ?? context.Settings.LocalAddress();

        var publisherAddress = PublisherAddress.CreateFromPhysicalAddresses(settings.BridgeAddress);
        publishers.AddOrReplacePublishers("Bridge", settings.PublisherTable.Select(kvp => new PublisherTableEntry(kvp.Key, publisherAddress)).ToList());

        context.Pipeline.Register(new RouteSiteMessagesToBridgeBehavior(settings.BridgeAddress), "Routes messages sent to sites to the bridge.");
        context.Pipeline.Register(new RoutingHeadersBehavior(settings.SendRouteTable), "Sets the ultimate destination endpoint on the outgoing messages.");
        context.Pipeline.Register(b => new BridgeSubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), settings.BridgeAddress, b.Build<IDispatchMessages>(), settings.PublisherTable, nativePubSub), 
            "Dispatches the subscribe request to the bridge.");
        context.Pipeline.Register(b => new BridgeUnsubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), settings.BridgeAddress, b.Build<IDispatchMessages>(), settings.PublisherTable, nativePubSub),
            "Dispatches the unsubscribe request to the bridge.");
    }
}
