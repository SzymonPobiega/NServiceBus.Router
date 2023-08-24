namespace NServiceBus.Router.AcceptanceTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using Configuration.AdvancedExtensibility;
    using Extensibility;
    using Features;
    using Microsoft.Extensions.DependencyInjection;
    using Persistence;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    public class InMemoryPersistence : PersistenceDefinition
    {
        internal InMemoryPersistence()
        {
            Supports<StorageType.Subscriptions>(s => { s.EnableFeatureByDefault<InMemorySubscriptionPersistence>(); });
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
#pragma warning disable 618
            DependsOn<MessageDrivenSubscriptions>();
#pragma warning restore 618
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var storageInstance = context.Settings.GetOrDefault<InMemorySubscriptionStorage>("InMemoryPersistence.StorageInstance");
            context.Services.AddSingleton<ISubscriptionStorage>(storageInstance ?? new InMemorySubscriptionStorage());
        }
    }

    public class InMemorySubscriptionStorage : ISubscriptionStorage
    {
        public Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken token)
        {
            var dict = storage.GetOrAdd(messageType, type => new ConcurrentDictionary<string, Subscriber>(StringComparer.OrdinalIgnoreCase));

            dict.AddOrUpdate(BuildKey(subscriber), _ => subscriber, (_, __) => subscriber);
            return Task.CompletedTask;
        }

        static string BuildKey(Subscriber subscriber)
        {
            return $"{subscriber.TransportAddress ?? ""}_{subscriber.Endpoint ?? ""}";
        }

        public Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken token)
        {
            if (storage.TryGetValue(messageType, out var dict))
            {
                dict.TryRemove(BuildKey(subscriber), out var _);
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageTypes, ContextBag context, CancellationToken token)
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
}