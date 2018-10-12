namespace NServiceBus.Router
{
    using System;
    using Resubscriber;
    using Transport;

    /// <summary>
    /// Configures resubscription feature that is required to keep events moving for broker transports
    /// that require re-subscribing to events after each endpoint restart (e.g. Azure Service Bus Endpoint-Oriented Topology)
    /// </summary>
    public static class ResubscriberExtensions
    {
        /// <summary>
        /// Enables resubscriber for a given interface.
        /// </summary>
        /// <typeparam name="T">Transport to use for the subscription storage.</typeparam>
        /// <param name="routerConfig">Config object.</param>
        /// <param name="interfaceName">Interface name.</param>
        /// <param name="delay">Resubscription delay.</param>
        /// <param name="customization">Transport customization.</param>
        public static void EnableResubscriber<T>(this RouterConfiguration routerConfig, string interfaceName, TimeSpan delay, Action<TransportExtensions<T>> customization)
            where  T : TransportDefinition, new()
        {
            var storageModule = new StorageModule<T>(interfaceName, delay, customization);
            routerConfig.AddModule(storageModule);

            var subscribeInterceptor = new SubscribeInterceptor(storageModule);
            var unsubscribeInterceptor = new UnsubscribeInterceptor(storageModule);

            routerConfig.AddRule(c => subscribeInterceptor, c => c.InetrfaceName == interfaceName);
            routerConfig.AddRule(c => unsubscribeInterceptor, c => c.InetrfaceName == interfaceName);

            routerConfig.AddRule(c => new SubscribeTerminator(c.Endpoint, c.TypeGenerator));
            routerConfig.DefineChain(cb => cb.Begin<SubscribeContext>().Terminate());
        }
    }
}