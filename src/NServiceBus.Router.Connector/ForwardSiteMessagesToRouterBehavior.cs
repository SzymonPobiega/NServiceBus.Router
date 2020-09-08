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

            var newRoutingStrategies = context.RoutingStrategies.Select(s => (RoutingStrategy)new SiteRoutingStrategy(s, state.Sites));
            context.RoutingStrategies = newRoutingStrategies.ToArray();
            return next();
        }

        public class State
        {
            public string[] Sites { get; set; }
        }

        class SiteRoutingStrategy : RoutingStrategy
        {
            public SiteRoutingStrategy(RoutingStrategy originalRoutingStrategy, string[] sites)
            {
                this.originalRoutingStrategy = originalRoutingStrategy;
                this.sites = sites;
            }

            public override AddressTag Apply(Dictionary<string, string> headers)
            {
                headers["NServiceBus.Bridge.DestinationSites"] = string.Join(";", sites);
                return originalRoutingStrategy.Apply(headers);
            }

            readonly RoutingStrategy originalRoutingStrategy;
            string[] sites;
        }
    }
}