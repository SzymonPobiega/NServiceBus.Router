using System.Linq;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class RouterConnectionFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var transportInfra = context.Settings.Get<TransportInfrastructure>();
        var nativePubSub = transportInfra.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Multicast;
        var settingsCollection = context.Settings.Get<RouterConnectionSettingsCollection>();
        var unicastRouteTable = context.Settings.Get<UnicastRoutingTable>();
        var bindings = context.Settings.Get<QueueBindings>();

        var compiledSettings = new CompiledRouterConnectionSettings(settingsCollection);

        foreach (var connection in settingsCollection.Connections)
        {
            //Make sure router queue does exist.
            bindings.BindSending(connection.RouterAddress);

            //Send the specified messages through the router
            var route = UnicastRoute.CreateFromPhysicalAddress(connection.RouterAddress);
            var routes = connection.SendRouteTable.Select(x => new RouteTableEntry(x.Key, route)).ToList();
            unicastRouteTable.AddOrReplaceRoutes("NServiceBus.Router_"+connection.RouterAddress, routes);
        }

        if (transportInfra.OutboundRoutingPolicy.Publishes == OutboundRoutingType.Unicast)
        {
            //Register the auto-publish-to-router behavior

            context.Pipeline.Register(b => new RouterAutoSubscribeBehavior(compiledSettings.AutoPublishRouters, b.Build<ISubscriptionStorage>()), "Automatically subscribes routers to published events.");
        }


        context.Pipeline.Register(new ForwardSiteMessagesToRouterBehavior(), "Routes messages sent to sites to the bridge.");
        context.Pipeline.Register(new RoutingHeadersBehavior(compiledSettings), "Sets the ultimate destination endpoint on the outgoing messages.");
        context.Pipeline.Register(new CorrelationIdForReplyBehavior(), "Copy previous correlation ID for reply");

        var isSendOnlyEndpoint = context.Settings.GetOrDefault<bool>("Endpoint.SendOnly");
        if (!isSendOnlyEndpoint)
        {
            var distributorAddress = context.Settings.GetOrDefault<string>("LegacyDistributor.Address");
            var subscriberAddress = distributorAddress ?? context.Settings.LocalAddress();

            context.Pipeline.Register(b => new RouterSubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), b.Build<IDispatchMessages>(), compiledSettings, nativePubSub),
                "Dispatches the subscribe request via a router.");
            context.Pipeline.Register(b => new RouterUnsubscribeBehavior(subscriberAddress, context.Settings.EndpointName(), b.Build<IDispatchMessages>(), compiledSettings, nativePubSub),
                "Dispatches the unsubscribe request via a router.");
        }
    }
}
