namespace NServiceBus.Router
{
    using System;
    using Raw;
    using Routing;
    using Transport;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    /// <summary>
    /// Configures the switch port.
    /// </summary>
    /// <typeparam name="T">Type of transport.</typeparam>
    public class InterfaceConfiguration<T>
        where T : TransportDefinition, new()
    {
        string Name;
        Action<TransportExtensions<T>> customization;
        bool? autoCreateQueues;
        string autoCreateQueuesIdentity;
        int? maximumConcurrency;
        ISubscriptionStorage subscriptionStorage;

        internal InterfaceConfiguration(string name, Action<TransportExtensions<T>> customization)
        {
            Name = name;
            this.customization = customization;
        }

        /// <summary>
        /// Configures the port to use specified subscription persistence.
        /// </summary>
        public void UseSubscriptionPersistence(ISubscriptionStorage subscriptionStorage)
        {
            this.subscriptionStorage = subscriptionStorage;
        }

        /// <summary>
        /// Configures the port to automatically create a queue when starting up. Overrides switch-level setting.
        /// </summary>
        /// <param name="identity">Identity to use when creating the queue.</param>
        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
        }

        /// <summary>
        /// Limits the processing concurrency of the port to a given value.
        /// </summary>
        /// <param name="maximumConcurrency">Maximum level of concurrency for the port's transport.</param>
        public void LimitMessageProcessingConcurrencyTo(int maximumConcurrency)
        {
            this.maximumConcurrency = maximumConcurrency;
        }

        /// <summary>
        /// Distribution policy of the port.
        /// </summary>
        public RawDistributionPolicy DistributionPolicy { get; } = new RawDistributionPolicy();

        /// <summary>
        /// Physical routing settings of the port.
        /// </summary>
        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        internal Interface Create(string endpointName, string poisonQueue, bool? hubAutoCreateQueues, string hubAutoCreateQueuesIdentity, int immediateRetries, int delayedRetries, int circuitBreakerThreshold)
        {
            IRuleCreationContext ContextFactory(IRawEndpoint e) => new RuleCreationContext(EndpointInstances, subscriptionStorage, DistributionPolicy, e);
            return new Interface<T>(endpointName, Name, customization, ContextFactory, poisonQueue, maximumConcurrency, autoCreateQueues ?? hubAutoCreateQueues ?? false, autoCreateQueuesIdentity ?? hubAutoCreateQueuesIdentity, immediateRetries, delayedRetries, circuitBreakerThreshold);
        }
    }
}
