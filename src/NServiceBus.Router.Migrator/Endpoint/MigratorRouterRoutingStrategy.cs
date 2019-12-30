namespace NServiceBus.Router.Migrator
{
    using System.Collections.Generic;
    using Routing;

    class MigratorRouterRoutingStrategy : RoutingStrategy
    {
        string routerAddress;
        UnicastRoutingStrategy unicastStrategy;

        public MigratorRouterRoutingStrategy(string routerAddress, UnicastRoutingStrategy unicastStrategy)
        {
            this.routerAddress = routerAddress;
            this.unicastStrategy = unicastStrategy;
        }

        public override AddressTag Apply(Dictionary<string, string> headers)
        {
            var unicastTag = (UnicastAddressTag)unicastStrategy.Apply(headers);
            headers["NServiceBus.Bridge.DestinationEndpoint"] = unicastTag.Destination;
            return new UnicastAddressTag(routerAddress);
        }
    }
}