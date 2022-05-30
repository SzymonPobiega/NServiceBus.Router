using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;

namespace NServiceBus
{
    /// <summary>
    /// Configures the router connection.
    /// </summary>
    public static class RouterConfigExtensions
    {
        /// <summary>
        /// Instructs the endpoint configuration to connect to a designated router. A single endpoint can connect to a single router.
        /// </summary>
        /// <param name="routingSettings">Routing settings.</param>
        /// <param name="routerAddress">Transport address of router's interface.</param>
        public static RouterConnectionSettings ConnectToRouter(this RoutingSettings routingSettings, string routerAddress)
        {
            return ConnectToRouterImpl(routingSettings, routerAddress, false, false);
        }

        /// <summary>
        /// Instructs the endpoint configuration to connect to a designated router. A single endpoint can connect to a single router.
        ///
        /// </summary>
        /// <param name="routingSettings">Routing settings.</param>
        /// <param name="routerAddress">Transport address of router's interface.</param>
        /// <param name="enableAutoSubscribe">Enable automatic subscription for this router. If enabled, the endpoint will ask the router to subscribe to all events handled by this endpoint. The subscription will only work if the other side of the router uses native PubSub transport or has AutoPublish enabled.</param>
        /// <param name="enableAutoPublish">Enable automatic publication forwarding of all published messages to the router. Applies only to transports that don't have native PubSub support.</param>
        public static RouterConnectionSettings ConnectToRouter(this RoutingSettings routingSettings, string routerAddress, bool enableAutoSubscribe, bool enableAutoPublish)
        {
            return ConnectToRouterImpl(routingSettings, routerAddress, enableAutoSubscribe, enableAutoPublish);
        }

        static RouterConnectionSettings ConnectToRouterImpl(RoutingSettings routingSettings, string routerAddress, bool enableAutoSubscribe, bool enableAutoPublish)
        {
            var settingsHolder = routingSettings.GetSettings();

            settingsHolder.EnableFeatureByDefault(typeof(RouterConnectionFeature));

            if (!settingsHolder.TryGet(out RouterConnectionSettingsCollection connectionSettings))
            {
                connectionSettings = new RouterConnectionSettingsCollection();
                settingsHolder.Set(connectionSettings);
            }

            return connectionSettings.GetOrCreate(routerAddress, enableAutoSubscribe, enableAutoPublish);
        }
    }
}