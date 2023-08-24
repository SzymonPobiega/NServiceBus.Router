using NServiceBus.Router;

public static class SubscriptionHelper
{
    public static InterfaceConfiguration InMemorySubscriptions(this InterfaceConfiguration interfaceConfig)
    {
        interfaceConfig.EnableMessageDrivenPublishSubscribe(new InMemorySubscriptionStorage());
        return interfaceConfig;
    }

    public static SendOnlyInterfaceConfiguration InMemorySubscriptions(this SendOnlyInterfaceConfiguration interfaceConfig)
    {
        interfaceConfig.EnableMessageDrivenPublishSubscribe(new InMemorySubscriptionStorage());
        return interfaceConfig;
    }
}