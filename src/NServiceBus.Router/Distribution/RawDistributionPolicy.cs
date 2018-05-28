namespace NServiceBus.Router
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Allows configuring distribution strategies for endpoints.
    /// </summary>
    public class RawDistributionPolicy
    {
        private ConcurrentDictionary<Tuple<string, DistributionStrategyScope>, RawDistributionStrategy> configuredStrategies = new ConcurrentDictionary<Tuple<string, DistributionStrategyScope>, RawDistributionStrategy>();

        /// <summary>Sets the distribution strategy for a given endpoint.</summary>
        /// <param name="distributionStrategy">Distribution strategy to be used.</param>
        public void SetDistributionStrategy(RawDistributionStrategy distributionStrategy)
        {
            if (distributionStrategy == null)
            {
                throw new ArgumentNullException(nameof(distributionStrategy));
            }
            this.configuredStrategies[Tuple.Create(distributionStrategy.Endpoint, distributionStrategy.Scope)] = distributionStrategy;
        }

        internal RawDistributionStrategy GetDistributionStrategy(string endpointName, DistributionStrategyScope scope)
        {
            return configuredStrategies.GetOrAdd(Tuple.Create(endpointName, scope), key => new SingleInstanceRoundRobinRawDistributionStrategy(key.Item1, key.Item2));
        }
    }
}