namespace NServiceBus.Router
{

    using System;
    using System.Collections.Generic;
    using Transport;

    /// <summary>
    /// Constructs the router.
    /// </summary>
    public class RouterConfiguration
    {
        /// <summary>
        /// Router endpoint name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Router's extensibility settings.
        /// </summary>
        public SettingsHolder Settings { get; } = new SettingsHolder();

        /// <summary>
        /// Creates new router configuration with provided endpoint name.
        /// </summary>
        /// <param name="name"></param>
        public RouterConfiguration(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Adds a new interface to the router.
        /// </summary>
        /// <param name="name">Name of the interface.</param>
        /// <param name="transport">Transport to use for this interface.</param>
        public InterfaceConfiguration AddInterface(string name, TransportDefinition transport)
        {
            var ifaceConfig = new InterfaceConfiguration(name, transport, this);
            InterfaceFactories.Add(() => CreateInterface(ifaceConfig));
            return ifaceConfig;
        }

        /// <summary>
        /// Adds a new send-only interface to the router.
        /// </summary>
        /// <param name="name">Name of the interface.</param>
        /// <param name="transport">Transport to use for this interface.</param>
        public SendOnlyInterfaceConfiguration AddSendOnlyInterface(string name, TransportDefinition transport)
        {
            var ifaceConfig = new SendOnlyInterfaceConfiguration(name, transport, this);
            SendOnlyInterfaceFactories.Add(() => CreateSendOnlyInterface(ifaceConfig));
            return ifaceConfig;
        }

        SendOnlyInterface CreateSendOnlyInterface(SendOnlyInterfaceConfiguration ifaceConfig)
        {
            return ifaceConfig.Create(Name, typeGenerator, Settings);
        }

        Interface CreateInterface(InterfaceConfiguration ifaceConfig)
        {
            return ifaceConfig.Create(Name, PoisonQueueName, autoCreateQueues, ImmediateRetries, DelayedRetries, CircuitBreakerThreshold, typeGenerator, Settings);
        }

        /// <summary>
        /// Configures the router to automatically create a queue when starting up.
        /// </summary>
        public void AutoCreateQueues()
        {
            autoCreateQueues = true;
        }

        /// <summary>
        /// Gets or sets the number of immediate retries to use when resolving failures during forwarding.
        /// </summary>
        public int ImmediateRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of delayed retries to use when resolving failures during forwarding.
        /// </summary>
        public int DelayedRetries { get; set; } = 5;

        /// <summary>
        /// Gets or sets the number of consecutive failures required to trigger the throttled mode.
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets the name of the poison queue.
        /// </summary>
        public string PoisonQueueName { get; set; } = "poison";

        /// <summary>
        /// Configures the routing protocol.
        /// </summary>
        public void UseRoutingProtocol(IRoutingProtocol protocol)
        {
            RoutingProtocol = protocol;
        }

        /// <summary>
        /// Adds a global (applicable to all interfaces) routing rule.
        /// </summary>
        /// <typeparam name="T">Type of the rule.</typeparam>
        /// <param name="constructor">Delegate that constructs a new instance of the rule.</param>
        /// <param name="condition">Condition which must be true for the rule to be added to the chain.</param>
        public void AddRule<T>(Func<IRuleCreationContext, T> constructor, Func<IRuleCreationContext, bool> condition = null)
            where T : IRule
        {
            Chains.AddRule(constructor, condition);
        }

        /// <summary>
        /// Adds a module.
        /// </summary>
        public void AddModule(IModule module)
        {
            Modules.Add(module);
        }

        /// <summary>
        /// Adds a feature.
        /// </summary>
        public void EnableFeature(Type featureType)
        {
            Features.Add(featureType);
        }

        /// <summary>
        /// Defines a custom chain within the router.
        /// </summary>
        /// <typeparam name="TInput">Input type of the chain.</typeparam>
        /// <param name="chainDefinition">Chain definition</param>
        public void DefineChain<TInput>(Func<ChainBuilder, IChain<TInput>> chainDefinition)
            where TInput : IRuleContext
        {
            Chains.AddChain(chainDefinition);
        }

        bool? autoCreateQueues;
        internal List<Func<Interface>> InterfaceFactories = new List<Func<Interface>>();
        internal List<Func<SendOnlyInterface>> SendOnlyInterfaceFactories = new List<Func<SendOnlyInterface>>();
        internal List<IModule> Modules = new List<IModule>();
        internal HashSet<Type> Features = new HashSet<Type>();
        internal IRoutingProtocol RoutingProtocol;
        internal InterfaceChains Chains = new InterfaceChains();
        RuntimeTypeGenerator typeGenerator = new RuntimeTypeGenerator();
    }
}