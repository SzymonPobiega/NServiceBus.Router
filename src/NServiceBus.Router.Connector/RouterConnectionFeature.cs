using System.Linq;
using System.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Transport;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

class RouterConnectionFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var transportDefinition = context.Settings.Get<TransportDefinition>();

        var nativePubSub = transportDefinition.SupportsPublishSubscribe;
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

        if (!nativePubSub || (context.Settings.TryGet("NServiceBus.Subscriptions.EnableMigrationMode", out bool enabled) && enabled))
        {
            //Register the auto-publish-to-router behavior

            context.Pipeline.Register(b => new RouterAutoSubscribeBehavior(compiledSettings.AutoPublishRouters, b.GetService<ISubscriptionStorage>()), "Automatically subscribes routers to published events.");
        }


        context.Pipeline.Register(new ForwardSiteMessagesToRouterBehavior(), "Routes messages sent to sites to the bridge.");
        context.Pipeline.Register(new RoutingHeadersBehavior(compiledSettings), "Sets the ultimate destination endpoint on the outgoing messages.");
        context.Pipeline.Register(new CorrelationIdForReplyBehavior(), "Copy previous correlation ID for reply");

        var isSendOnlyEndpoint = context.Settings.GetOrDefault<bool>("Endpoint.SendOnly");
        if (!isSendOnlyEndpoint)
        {
            var subscriberAddress = context.LocalQueueAddress();

            context.Pipeline.Register(b =>
                {
                    var resolver = b.GetService<ITransportAddressResolver>();
                    return new RouterSubscribeBehavior(resolver.ToTransportAddress(subscriberAddress), context.Settings.EndpointName(), b.GetService<IMessageDispatcher>(), compiledSettings, nativePubSub);
                },
                "Dispatches the subscribe request via a router.");
            context.Pipeline.Register(b =>
                {
                    var resolver = b.GetService<ITransportAddressResolver>();
                    return new RouterUnsubscribeBehavior(resolver.ToTransportAddress(subscriberAddress), context.Settings.EndpointName(), b.GetService<IMessageDispatcher>(), compiledSettings, nativePubSub);
                },
                "Dispatches the unsubscribe request via a router.");
        }
    }
}
