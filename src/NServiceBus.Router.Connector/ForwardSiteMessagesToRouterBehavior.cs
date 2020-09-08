namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Pipeline;
    using Routing;

    class ForwardSiteMessagesToRouterBehavior : Behavior<IRoutingContext>
    {
        readonly CompiledRouterConnectionSettings compiledSettings;

        public ForwardSiteMessagesToRouterBehavior(CompiledRouterConnectionSettings compiledSettings)
        {
            this.compiledSettings = compiledSettings;
        }

        public override Task Invoke(IRoutingContext context, Func<Task> next)
        {
            if (!context.Extensions.TryGet<State>(out var state))
            {
                return next();
            }
            if (state.Sites.Any(s => s.Contains(";")))
            {
                throw new Exception("Site name cannot contain a semicolon.");
            }

            var newRoutingStrategies = context.RoutingStrategies
                .SelectMany(s => CreateSiteRoutingStrategies(s, state));
            context.RoutingStrategies = newRoutingStrategies.ToArray();
            return next();
        }

        IEnumerable<RoutingStrategy> CreateSiteRoutingStrategies(RoutingStrategy routingStrategy, State state)
        {
            var map = state.Sites.ToDictionary(x => x, x => compiledSettings.GetRouterForSite(x));
            var lookup = map.ToLookup(x => x.Value, x => x.Key);
            return lookup.Select(x => new SiteRoutingStrategy(x.Key, x.ToArray()));
        }

        public class State
        {
            public string[] Sites { get; set; }
        }

        class SiteRoutingStrategy : RoutingStrategy
        {
            public SiteRoutingStrategy(string routerAddress, string[] sites)
            {
                this.routerAddress = routerAddress;
                this.sites = sites;
            }

            public override AddressTag Apply(Dictionary<string, string> headers)
            {
                headers["NServiceBus.Bridge.DestinationSites"] = string.Join(";", sites);
                return new UnicastAddressTag(routerAddress);
            }

            string routerAddress;
            string[] sites;
        }
    }
}