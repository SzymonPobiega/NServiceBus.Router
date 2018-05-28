using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;

namespace NServiceBus
{
    /// <summary>
    /// Allows co
    /// </summary>
    public static class BridgeRoutingExtensions
    {
        /// <summary>
        /// Instructs the endpoint configuration to connect to a designated bridge. A single endpoint can connect to a single bridge.
        /// </summary>
        /// <param name="routingSettings">Routing settings.</param>
        /// <param name="bridgeAddress">Transport address of the bridge.</param>
        public static BridgeRoutingSettings ConnectToBridge(this RoutingSettings routingSettings, string bridgeAddress)
        {
            routingSettings.GetSettings().EnableFeatureByDefault(typeof(BridgeRoutingFeature));

            var settings = new BridgeRoutingSettings(bridgeAddress);
            routingSettings.GetSettings().Set<BridgeRoutingSettings>(settings);
            return settings;
        }
    }
}