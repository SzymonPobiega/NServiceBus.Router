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
            routingSettings.GetSettings().EnableFeatureByDefault(typeof(RouterConnectionFeature));

            var settings = new RouterConnectionSettings(routerAddress);
            routingSettings.GetSettings().Set<RouterConnectionSettings>(settings);
            return settings;
        }
    }
}