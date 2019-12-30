namespace NServiceBus.Router.Migrator
{
    using System.Collections.Generic;
    using Routing;

    class DualRoutingMulticastRoutingStrategy : RoutingStrategy
    {
        MulticastRoutingStrategy multicastStrategy;
        string[] ignores;

        public DualRoutingMulticastRoutingStrategy(string[] ignores, MulticastRoutingStrategy multicastStrategy)
        {
            this.ignores = ignores;
            this.multicastStrategy = multicastStrategy;
        }

        public override AddressTag Apply(Dictionary<string, string> headers)
        {
            //Todo replace with multi header
            headers.Add("NServiceBus.Router.Migrator.Ignore", string.Join("|", ignores));
            return multicastStrategy.Apply(headers);
        }
    }
}