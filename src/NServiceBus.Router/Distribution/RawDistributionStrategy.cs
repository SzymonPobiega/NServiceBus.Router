namespace NServiceBus.Router
{
    using System;

    /// <summary>
    /// Determines which instance of a given endpoint a message is to be sent.
    /// </summary>
    public abstract class RawDistributionStrategy
    {
        /// <summary>
        /// Creates a new <see cref="RawDistributionStrategy"/>.
        /// </summary>
        /// <param name="endpoint">The name of the endpoint this distribution strategy resolves instances for.</param>
        /// <param name="scope">The scope for this strategy.</param>
        protected RawDistributionStrategy(string endpoint, DistributionStrategyScope scope)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Scope = scope;
        }

        /// <summary>
        /// Selects a destination instance for a message from all known addresses of a logical endpoint.
        /// </summary>
        public abstract string SelectDestination(string[] candidates);

        /// <summary>
        /// The name of the endpoint this distribution strategy resolves instances for.
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// The scope of this strategy.
        /// </summary>
        public DistributionStrategyScope Scope { get; }
    }
}