﻿namespace NServiceBus.Router
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;
    using Logging;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

#pragma warning disable 618

    /// <summary>
    /// SQL-based subscription persistence.
    /// </summary>
    public class SqlSubscriptionStorage : ISubscriptionStorage
    {
        /// <summary>
        /// Creates new instance of SQL-based subscription persistence.
        /// </summary>
        /// <param name="connectionBuilder">A func that returns a not-yet-opened connection to the database.</param>
        /// <param name="tablePrefix"></param>
        /// <param name="sqlDialect"></param>
        /// <param name="cacheFor"></param>
        public SqlSubscriptionStorage(Func<DbConnection> connectionBuilder, string tablePrefix, SqlDialect sqlDialect, TimeSpan? cacheFor)
        {
            this.connectionOpener = () => connectionBuilder.OpenConnection();
            this.tablePrefix = tablePrefix;
            this.sqlDialect = sqlDialect;
            this.cacheFor = cacheFor;
            subscriptionCommands = SubscriptionCommandBuilder.Build(sqlDialect, tablePrefix);
            if (cacheFor != null)
            {
                cache = new ConcurrentDictionary<string, CacheItem>();
            }
        }

        /// <summary>
        /// Creates new instance of SQL-based subscription persistence.
        /// </summary>
        /// <param name="asyncConnectionBuilder">A func that returns an already open connection to the database.</param>
        /// <param name="tablePrefix"></param>
        /// <param name="sqlDialect"></param>
        /// <param name="cacheFor"></param>
        public SqlSubscriptionStorage(Func<Task<DbConnection>> asyncConnectionBuilder, string tablePrefix, SqlDialect sqlDialect, TimeSpan? cacheFor)
        {
            this.connectionOpener = asyncConnectionBuilder;
            this.tablePrefix = tablePrefix;
            this.sqlDialect = sqlDialect;
            this.cacheFor = cacheFor;
            subscriptionCommands = SubscriptionCommandBuilder.Build(sqlDialect, tablePrefix);
            if (cacheFor != null)
            {
                cache = new ConcurrentDictionary<string, CacheItem>();
            }
        }

        /// <summary>
        /// Creates the required schema objects.
        /// </summary>
        public async Task Install()
        {
            using (var connection = await connectionOpener().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                await sqlDialect.ExecuteTableCommand(connection, transaction, SubscriptionScriptBuilder.BuildCreateScript(sqlDialect), tablePrefix);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Drops the required schema objects.
        /// </summary>
        public async Task Uninstall()
        {
            using (var connection = await connectionOpener().ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                await sqlDialect.ExecuteTableCommand(connection, transaction, SubscriptionScriptBuilder.BuildDropScript(sqlDialect), tablePrefix);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Subscribes the given client to messages of a given type.
        /// </summary>
        public async Task Subscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
        {
            await Retry(async () =>
            {
                using (var connection = await connectionOpener().ConfigureAwait(false))
                using (var command = sqlDialect.CreateCommand(connection))
                {
                    command.CommandText = subscriptionCommands.Subscribe;
                    command.AddParameter("MessageType", messageType.TypeName);
                    command.AddParameter("Subscriber", subscriber.TransportAddress);
                    command.AddParameter("Endpoint", Nullable(subscriber.Endpoint));
                    command.AddParameter("PersistenceVersion", StaticVersions.PersistenceVersion);
                    await command.ExecuteNonQueryEx().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
            ClearForMessageType(messageType);
        }

        /// <summary>
        /// Unsubscribes the given client from messages of given type.
        /// </summary>
        public async Task Unsubscribe(Subscriber subscriber, MessageType messageType, ContextBag context, CancellationToken cancellationToken = default)
        {
            await Retry(async () =>
            {
                using (var connection = await connectionOpener().ConfigureAwait(false))
                using (var command = sqlDialect.CreateCommand(connection))
                {
                    command.CommandText = subscriptionCommands.Unsubscribe;
                    command.AddParameter("MessageType", messageType.TypeName);
                    command.AddParameter("Subscriber", subscriber.TransportAddress);
                    await command.ExecuteNonQueryEx().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
            ClearForMessageType(messageType);
        }

        /// <summary>
        /// Returns a list of addresses for subscribers currently subscribed to the given message type.
        /// </summary>
        public Task<IEnumerable<Subscriber>> GetSubscriberAddressesForMessage(IEnumerable<MessageType> messageHierarchy, ContextBag context, CancellationToken cancellationToken = default)
        {
            var types = messageHierarchy.ToList();

            if (cacheFor == null)
            {
                return GetSubscriptions(types);
            }

            var key = GetKey(types);

            var cacheItem = cache.GetOrAdd(key,
                valueFactory: _ => new CacheItem
                {
                    Stored = DateTime.UtcNow,
                    Subscribers = GetSubscriptions(types)
                });

            var age = DateTime.UtcNow - cacheItem.Stored;
            if (age >= cacheFor)
            {
                cacheItem.Subscribers = GetSubscriptions(types);
                cacheItem.Stored = DateTime.UtcNow;
            }
            return cacheItem.Subscribers;
        }

        static object Nullable(object value)
        {
            return value ?? DBNull.Value;
        }

        static async Task Retry(Func<Task> action)
        {
            var attempts = 0;
            while (true)
            {
                try
                {
                    await action().ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    attempts++;

                    if (attempts > 10)
                    {
                        throw;
                    }
                    Log.Debug("Error while processing subscription change request. Retrying.", ex);
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
        }

        void ClearForMessageType(MessageType messageType)
        {
            if (cacheFor == null)
            {
                return;
            }
            var keyPart = GetKeyPart(messageType);
            foreach (var cacheKey in cache.Keys)
            {
                if (cacheKey.Contains(keyPart))
                {
                    cache.TryRemove(cacheKey, out _);
                }
            }
        }

        static string GetKey(List<MessageType> types)
        {
            var typeNames = types.Select(_ => _.TypeName);
            return string.Join(",", typeNames) + ",";
        }

        static string GetKeyPart(MessageType type)
        {
            return $"{type.TypeName},";
        }

        async Task<IEnumerable<Subscriber>> GetSubscriptions(List<MessageType> messageHierarchy)
        {
            var getSubscribersCommand = subscriptionCommands.GetSubscribers(messageHierarchy);
            using (var connection = await connectionOpener().ConfigureAwait(false))
            using (var command = sqlDialect.CreateCommand(connection))
            {
                for (var i = 0; i < messageHierarchy.Count; i++)
                {
                    var messageType = messageHierarchy[i];
                    var paramName = $"type{i}";
                    command.AddParameter(paramName, messageType.TypeName);
                }
                command.CommandText = getSubscribersCommand;
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    var subscribers = new List<Subscriber>();
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var address = reader.GetString(0);
                        string endpoint;
                        if (await reader.IsDBNullAsync(1).ConfigureAwait(false))
                        {
                            endpoint = null;
                        }
                        else
                        {
                            endpoint = reader.GetString(1);
                        }
                        subscribers.Add(new Subscriber(address, endpoint));
                    }
                    return subscribers;
                }
            }
        }

        ConcurrentDictionary<string, CacheItem> cache;
        Func<Task<DbConnection>> connectionOpener;
        string tablePrefix;
        SqlDialect sqlDialect;
        TimeSpan? cacheFor;
        SubscriptionCommands subscriptionCommands;
        static ILog Log = LogManager.GetLogger<SqlSubscriptionStorage>();

        class CacheItem
        {
            public DateTime Stored;
            public Task<IEnumerable<Subscriber>> Subscribers;
        }
    }
}