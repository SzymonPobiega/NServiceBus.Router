namespace NServiceBus.Router
{
    using System;
    using System.Linq;

    /// <summary>
    /// Allows creating routers.
    /// </summary>
    public static class Router
    {
        /// <summary>
        /// Creates a new instance of a router based on the provided configuration.
        /// </summary>
        /// <param name="config">Router configuration.</param>
        public static IRouter Create(RouterConfiguration config)
        {
            if (config.RoutingProtocol == null)
            {
                throw new Exception("Routing protocol must be configured.");
            }

            var ports = config.PortFactories.Select(x => x()).ToArray();
            return new RouterImpl(config.Name, ports, config.RoutingProtocol);
        }
    }
}