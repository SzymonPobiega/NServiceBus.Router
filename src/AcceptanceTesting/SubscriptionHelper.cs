using NServiceBus.Router;
using NServiceBus.Transport;

public static class SubscriptionHelper
{
    public static InterfaceConfiguration<T> InMemorySubscriptions<T>(this InterfaceConfiguration<T> interfaceConfig) where T : TransportDefinition, new()
    {
        interfaceConfig.UseSubscriptionPersistence(new InMemorySubscriptionStorage());
        return interfaceConfig;
    }
}