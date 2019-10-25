using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Extensibility;
using NServiceBus.Features;
using NServiceBus.Persistence;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;

public class InMemoryPersistence : PersistenceDefinition
{
    internal InMemoryPersistence()
    {
        Supports<StorageType.Subscriptions>(s =>
        {
            s.EnableFeatureByDefault<InMemorySubscriptionPersistence>();
        });
    }
}

public static class InMemoryPersistenceExtensions
{
    public static void UseStorage(this PersistenceExtensions<InMemoryPersistence> extensions, InMemorySubscriptionStorage storageInstance)
    {
        extensions.GetSettings().Set("InMemoryPersistence.StorageInstance", storageInstance);
    }
}

public class InMemorySubscriptionPersistence : Feature
{
    internal InMemorySubscriptionPersistence()
    {
        DependsOn<MessageDrivenSubscriptions>();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var storageInstance = context.Settings.GetOrDefault<InMemorySubscriptionStorage>("InMemoryPersistence.StorageInstance");
        context.Container.RegisterSingleton<ISubscriptionStorage>(storageInstance ?? new InMemorySubscriptionStorage());
    }
}

public class InMemorySubscriptionStorage : ISubscriptionStorage
{
    public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
    {
        var dict = storage.GetOrAdd(messageType, type => new ConcurrentDictionary<string, Subscriber>(StringComparer.OrdinalIgnoreCase));

        dict.AddOrUpdate(BuildKey(subscriber), _ => subscriber, (_, __) => subscriber);
        return Task.CompletedTask;
    }

    static string BuildKey(Subscriber subscriber)
    {
        return $"{subscriber.TransportAddress ?? ""}_{subscriber.Endpoint ?? ""}";
    }

    public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context)
    {
        if (storage.TryGetValue(messageType, out var dict))
        {
            dict.TryRemove(BuildKey(subscriber), out var _);
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context)
    {
        var result = new HashSet<Subscriber>();
        foreach (var m in messageTypes)
        {
            if (storage.TryGetValue(m, out var list))
            {
                result.UnionWith(list.Values);
            }
        }
        return Task.FromResult((IEnumerable<Subscriber>)result);
    }

    ConcurrentDictionary<MessageType, ConcurrentDictionary<string, Subscriber>> storage = new ConcurrentDictionary<MessageType, ConcurrentDictionary<string, Subscriber>>();
}
