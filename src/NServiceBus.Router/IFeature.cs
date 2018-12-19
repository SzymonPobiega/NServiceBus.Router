namespace NServiceBus.Router
{
    /// <summary>
    /// Represents a feature.
    /// </summary>
    public interface IFeature
    {
        /// <summary>
        /// Configures the router for this feature.
        /// </summary>
        /// <param name="routerConfig"></param>
        void Configure(RouterConfiguration routerConfig);
    }
}