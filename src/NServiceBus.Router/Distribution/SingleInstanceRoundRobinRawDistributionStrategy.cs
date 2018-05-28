namespace NServiceBus.Router
{
    using System.Threading;

    /// <summary>
    /// A default distribution strategy that sends messages to multiple instances of a destination endpoint in a round-robin manner.
    /// </summary>
    public class SingleInstanceRoundRobinRawDistributionStrategy : RawDistributionStrategy
    {
        long index = -1;

        /// <summary>
        /// Creates a new <see cref="T:NServiceBus.Routing.SingleInstanceRoundRobinDistributionStrategy" /> instance.
        /// </summary>
        /// <param name="endpoint">The name of the endpoint this distribution strategy resolves instances for.</param>
        /// <param name="scope">The scope for this strategy.</param>
        public SingleInstanceRoundRobinRawDistributionStrategy(string endpoint, DistributionStrategyScope scope)
            : base(endpoint, scope)
        {
        }

        /// <summary>
        /// Selects a destination instance for a message from all known addresses of a logical endpoint.
        /// </summary>
        public override string SelectDestination(string[] candidates)
        {
            if (candidates.Length == 0)
            {
                return null;
            }
            var i = Interlocked.Increment(ref index);
            var result = candidates[(int)(i % candidates.Length)];
            return result;
        }
    }
}