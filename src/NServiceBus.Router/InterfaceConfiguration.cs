namespace NServiceBus.Router
{
    using System;
    using Raw;
    using Routing;
    using Transport;

    /// <summary>
    /// Configures the switch port.
    /// </summary>
    public class InterfaceConfiguration
    {
        bool? autoCreateQueues;
        int? maximumConcurrency;
        string overriddenEndpointName;

        /// <summary>
        /// Interface's extensibility settings.
        /// </summary>
        public SettingsHolder Settings { get; } = new SettingsHolder();

        /// <summary>
        /// Name of the interface.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Router's configuration.
        /// </summary>
        public RouterConfiguration RouterConfiguration { get; }

        /// <summary>
        /// Transport used by this interface.
        /// </summary>
        public TransportDefinition Transport { get; }


        internal InterfaceConfiguration(string name, TransportDefinition transport, RouterConfiguration routerConfiguration)
        {
            this.Transport = transport;
            Name = name;
            RouterConfiguration = routerConfiguration;
        }

        /// <summary>
        /// Adds routing rule that applies only to this interface.
        /// </summary>
        /// <typeparam name="TRule">Type of the rule.</typeparam>
        /// <param name="constructor">Delegate that constructs a new instance of the rule.</param>
        /// <param name="condition">Condition which must be true for the rule to be added to the chain.</param>
        public void AddRule<TRule>(Func<IRuleCreationContext, TRule> constructor, Func<IRuleCreationContext, bool> condition = null)
            where TRule : IRule
        {
            RouterConfiguration.AddRule(constructor, context =>
            {
                if (condition == null)
                {
                    return context.InterfaceName == Name;
                }
                return condition(context) && context.InterfaceName == Name;
            });
        }

        /// <summary>
        /// Configures the port to automatically create a queue when starting up. Overrides switch-level setting.
        /// </summary>
        public void AutoCreateQueues()
        {
            autoCreateQueues = true;
        }

        /// <summary>
        /// Overrides the interface endpoint name.
        /// </summary>
        /// <param name="interfaceEndpointName">Endpoint name to use for this interface instead of Router's name</param>
        public void OverrideEndpointName(string interfaceEndpointName)
        {
            overriddenEndpointName = interfaceEndpointName;
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

        internal Interface Create(string endpointName, string poisonQueue, bool? routerAutoCreateQueues, int immediateRetries, int delayedRetries, int circuitBreakerThreshold, RuntimeTypeGenerator typeGenerator, SettingsHolder routerSettings)
        {
            IRuleCreationContext ContextFactory(IRawEndpoint e)
            {
                Settings.Merge(routerSettings);
                return new RuleCreationContext(Name, EndpointInstances, DistributionPolicy, e, typeGenerator, Settings);
            }

            return new Interface(overriddenEndpointName ?? endpointName, Name, Transport, ContextFactory, poisonQueue, maximumConcurrency, autoCreateQueues ?? routerAutoCreateQueues ?? false, immediateRetries, delayedRetries, circuitBreakerThreshold);
        }
    }
}
