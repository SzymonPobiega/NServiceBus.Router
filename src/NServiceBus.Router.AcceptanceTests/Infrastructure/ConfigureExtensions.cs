namespace NServiceBus.Router.AcceptanceTests
{
    using Configuration.AdvancedExtensibility;
    using Transport;

    public static class ConfigureExtensions
    {
        public static RoutingSettings ConfigureRouting(this EndpointConfiguration configuration) =>
            new RoutingSettings(configuration.GetSettings());

        public static TransportDefinition ConfigureTransport(this EndpointConfiguration configuration) =>
            configuration.GetSettings().Get<TransportDefinition>();

        public static BrokerSettings ConfigureBroker(this EndpointConfiguration configuration)
        {
            return new BrokerSettings((AcceptanceTestingTransport)configuration.GetSettings().Get<TransportDefinition>());
        }

        public static TTransportDefinition ConfigureTransport<TTransportDefinition>(
            this EndpointConfiguration configuration)
            where TTransportDefinition : TransportDefinition =>
            (TTransportDefinition)configuration.GetSettings().Get<TransportDefinition>();

        public static BrokerSettings Broker(this InterfaceConfiguration iface)
        {
            return new BrokerSettings((AcceptanceTestingTransport)iface.Transport);
        }

        public static BrokerSettings Broker(this AcceptanceTestingTransport transport)
        {
            return new BrokerSettings(transport);
        }

        public static InterfaceConfiguration AddInterface(this RouterConfiguration cfg, string name, bool nativePubSub = true)
        {
            var transport = new AcceptanceTestingTransport(true, nativePubSub);
            transport.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

            var @interface = cfg.AddInterface(name, transport);

            if (!transport.SupportsPublishSubscribe)
            {
                @interface.InMemorySubscriptions();
            }

            return @interface;
        }
    }
}