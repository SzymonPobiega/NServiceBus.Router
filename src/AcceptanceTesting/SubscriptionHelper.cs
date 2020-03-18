using NServiceBus.Router;
using NServiceBus.Transport;

public static class SubscriptionHelper
{
    public static InterfaceConfiguration<T> InMemorySubscriptions<T>(this InterfaceConfiguration<T> interfaceConfig) where T : TransportDefinition, new()
    {
        interfaceConfig.EnableMessageDrivenPublishSubscribe(new InMemorySubscriptionStorage());
        return interfaceConfig;
    }

    public static SendOnlyInterfaceConfiguration<T> InMemorySubscriptions<T>(this SendOnlyInterfaceConfiguration<T> interfaceConfig) where T : TransportDefinition, new()
    {
        interfaceConfig.EnableMessageDrivenPublishSubscribe(new InMemorySubscriptionStorage());
        return interfaceConfig;
    }
}