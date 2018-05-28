namespace NServiceBus.Router
{
    using System.Threading.Tasks;

    /// <summary>
    /// Configures the router to use static routing.
    /// </summary>
    public static class StaticRoutingProtocolExtensions
    {
        /// <summary>
        /// Configures the router to use static routing.
        /// </summary>
        /// <returns></returns>
        public static RouteTable UseStaticRoutingProtocol(this RouterConfiguration config)
        {
            if (config.RoutingProtocol is StaticRoutingProtocol existing)
            {
                return existing.RouteTable;
            }
            var protocol = new StaticRoutingProtocol();
            config.UseRoutingProtocol(protocol);
            return protocol.RouteTable;
        }

        class StaticRoutingProtocol : IRoutingProtocol
        {
            public Task Start(RouterMetadata metadata)
            {
                return Task.CompletedTask;
            }

            public Task Stop()
            {
                return Task.CompletedTask;
            }

            public RouteTable RouteTable { get; } = new RouteTable();
        }
    }
}