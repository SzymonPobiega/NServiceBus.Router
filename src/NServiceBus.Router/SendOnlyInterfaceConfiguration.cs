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
    public class SendOnlyInterfaceConfiguration<T>
        where T : TransportDefinition, new()
    {
        Action<TransportExtensions<T>> customization;
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

        internal SendOnlyInterfaceConfiguration(string name, Action<TransportExtensions<T>> customization, RouterConfiguration routerConfiguration)
        {
            Name = name;
            this.customization = customization;
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
        /// Configures the port to use specified subscription persistence.
        /// </summary>
        [Obsolete("Use EnableMessageDrivenPublishSubscribe instead.")]
        public void UseSubscriptionPersistence(ISubscriptionStorage subscriptionStorage)
        {
            this.EnableMessageDrivenPublishSubscribe(subscriptionStorage);
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

        internal Interface Create(string endpointName, RuntimeTypeGenerator typeGenerator, SettingsHolder routerSettings)
        {
            IRuleCreationContext ContextFactory(IRawEndpoint e)
            {
                Settings.Merge(routerSettings);
                return new RuleCreationContext(Name, EndpointInstances, DistributionPolicy, e, typeGenerator, Settings);
            }

            return new SendOnlyInterface<T>(overriddenEndpointName ?? endpointName, Name, customization, ContextFactory);
        }
    }
}
