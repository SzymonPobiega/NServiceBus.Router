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
        /// <typeparam name="T">Transport to use for this interface.</typeparam>
        /// <param name="name">Name of the interface.</param>
        /// <param name="customization">A callback for customizing the transport settings.</param>
        public InterfaceConfiguration<T> AddInterface<T>(string name, Action<TransportExtensions<T>> customization) 
            where T : TransportDefinition, new()
        {
            var ifaceConfig = new InterfaceConfiguration<T>(name, customization);
            PortFactories.Add(() => CreateInterface(ifaceConfig));
            return ifaceConfig;
        }

        Interface CreateInterface<T>(InterfaceConfiguration<T> ifaceConfig) where T : TransportDefinition, new()
        {
            return ifaceConfig.Create(Name, typeGenerator, "poison", autoCreateQueues, autoCreateQueuesIdentity, InterceptMethod, () => RoutingProtocol.RouteTable, ImmediateRetries, DelayedRetries, CircuitBreakerThreshold);
        }

        /// <summary>
        /// Configures the router to automatically create a queue when starting up.
        /// </summary>
        /// <param name="identity">Identity to use when creating the queue.</param>
        public void AutoCreateQueues(string identity = null)
        {
            autoCreateQueues = true;
            autoCreateQueuesIdentity = identity;
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
        /// Configures the router to invoke a provided callback when processing messages.
        /// </summary>
        /// <param name="interceptMethod">Callback to be invoked.</param>
        public void InterceptForwarding(InterceptMessageForwarding interceptMethod)
        {
            InterceptMethod = interceptMethod ?? throw new ArgumentNullException(nameof(interceptMethod));
        }
        
        /// <summary>
        /// Configures the routing protocol.
        /// </summary>
        public void UseRoutingProtocol(IRoutingProtocol protocol)
        {
            RoutingProtocol = protocol;
        }

        InterceptMessageForwarding InterceptMethod = (queue, message, dispatch, forward) => forward(dispatch);
        bool? autoCreateQueues;
        string autoCreateQueuesIdentity;
        RuntimeTypeGenerator typeGenerator = new RuntimeTypeGenerator();
        internal List<Func<Interface>> PortFactories = new List<Func<Interface>>();
        internal IRoutingProtocol RoutingProtocol;
    }
}