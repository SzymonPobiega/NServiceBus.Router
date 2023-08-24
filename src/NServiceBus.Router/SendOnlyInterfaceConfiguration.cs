namespace NServiceBus.Router
{
    using System;
    using Raw;
    using Routing;
    using Transport;

    /// <summary>
    /// Configures the switch port.
    /// </summary>
    public class SendOnlyInterfaceConfiguration
    {
        TransportDefinition transport;
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

        internal SendOnlyInterfaceConfiguration(string name, TransportDefinition transport, RouterConfiguration routerConfiguration)
        {
            this.transport = transport;
            Name = name;
            RouterConfiguration = routerConfiguration;
        }

        /// <summary>
        /// Adds a global (applicable to all interfaces) routing rule.
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
        /// Overrides the interface endpoint name.
        /// </summary>
        /// <param name="interfaceEndpointName">Endpoint name to use for this interface instead of Router's name</param>
        public void OverrideEndpointName(string interfaceEndpointName)
        {
            overriddenEndpointName = interfaceEndpointName;
        }

        /// <summary>
        /// Distribution policy of the port.
        /// </summary>
        public RawDistributionPolicy DistributionPolicy { get; } = new RawDistributionPolicy();

        /// <summary>
        /// Physical routing settings of the port.
        /// </summary>
        public EndpointInstances EndpointInstances { get; } = new EndpointInstances();

        internal SendOnlyInterface Create(string endpointName, RuntimeTypeGenerator typeGenerator, SettingsHolder routerSettings)
        {
            IRuleCreationContext ContextFactory(IRawEndpoint e)
            {
                Settings.Merge(routerSettings);
                return new RuleCreationContext(Name, EndpointInstances, DistributionPolicy, e, typeGenerator, Settings);
            }

            return new SendOnlyInterface(overriddenEndpointName ?? endpointName, Name, transport, ContextFactory);
        }
    }
}
