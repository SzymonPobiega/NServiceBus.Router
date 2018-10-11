using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace NServiceBus.Router.Resubscriber
{
    class StorageModule<T> : IModule, IResubscriberStorage
        where T : TransportDefinition, new()
    {
        static ILog logger = LogManager.GetLogger<StorageModule<T>>();

        readonly string interfaceName;

        Dictionary<string, Tuple<string, DateTime>> idMap = new Dictionary<string, Tuple<string, DateTime>>();
        RawEndpointConfiguration config;
        IReceivingRawEndpoint stoppable;
        IChain<SubscribeContext> subscribeChain;
        CancellationTokenSource tokenSource = new CancellationTokenSource();

        public StorageModule(string interfaceName, TimeSpan delay, Action<TransportExtensions<T>> configureTransport)
        {
            this.interfaceName = interfaceName;
            var resubscriberQueue = interfaceName + ".Resubscriber";
            config = RawEndpointConfiguration.Create(resubscriberQueue,
                async (context, dispatcher) =>
                {
                    if (!context.Headers.TryGetValue(ResubscriptionIdHeader, out var resubscriptionId)
                        || !context.Headers.TryGetValue(ResubscriptionTimestampHeader, out var resubscriptionTimestampString)
                        || !context.Headers.TryGetValue(ResubscriptionActionHeader, out var resubscriptionIntent)
                        || !context.Headers.TryGetValue(ResubscriptionTypeHeader, out var messageTypeString))
                    {
                        //Ignore messages with missing headers
                        return;
                    }

                    var resubscriptionTimestamp = DateTime.Parse(resubscriptionTimestampString);
                    if (idMap.ContainsKey(messageTypeString))
                    {
                        var valuePair = idMap[messageTypeString];
                        if (valuePair.Item1 != resubscriptionId && resubscriptionTimestamp < valuePair.Item2)
                        {
                            //If we already processed a newer subscribe/unsubscribe message for this -> ignore
                            return;
                        }
                        //We've seen that same message before. Let's pause the resubscription for some time
                        await Task.Delay(delay, tokenSource.Token);
                    }

                    if (!tokenSource.IsCancellationRequested)
                    {
                        if (ActionSubscribe.Equals(resubscriptionIntent, StringComparison.InvariantCultureIgnoreCase))
                        {
                            await subscribeChain.Invoke(new SubscribeContext(messageTypeString));
                        }
                        //We don't need to re-unsubscribe
                    }

                    //Move to the end of the queue
                    var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
                    var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(resubscriberQueue));

                    await dispatcher.Dispatch(new TransportOperations(operation), context.TransportTransaction, context.Extensions).ConfigureAwait(false);
                    logger.Debug("Moved subscription message back to the queue.");
                    idMap[messageTypeString] = Tuple.Create(resubscriptionId, resubscriptionTimestamp);
                }, "poison");
            config.AutoCreateQueue();
            config.LimitMessageProcessingConcurrencyTo(1);
            var transport = config.UseTransport<T>();
            configureTransport(transport);
        }

        internal async Task Enqueue(string messageType, string action, DateTime timestamp, string id)
        {
            var outgoingHeaders = new Dictionary<string, string>
            {
                [ResubscriptionIdHeader] = id,
                [ResubscriptionTimestampHeader] = timestamp.ToString("O"),
                [ResubscriptionTypeHeader] = messageType,
                [ResubscriptionActionHeader] = action,
            };

            var outgoingMessage = new OutgoingMessage(Guid.NewGuid().ToString(), outgoingHeaders, new byte[0]);
            var operation = new TransportOperation(outgoingMessage, new UnicastAddressTag(stoppable.TransportAddress));

            await stoppable.Dispatch(new TransportOperations(operation), new TransportTransaction(), new ContextBag())
                .ConfigureAwait(false);
            logger.Debug("Stored resubscription message in the resubscriber queue.");
        }

        public async Task Start(RootContext rootContext)
        {
            subscribeChain = rootContext.Interfaces.GetChainsFor(interfaceName).Get<SubscribeContext>();

            var endpoint = await RawEndpoint.Create(config).ConfigureAwait(false);
            stoppable = await endpoint.Start().ConfigureAwait(false);
        }

        public async Task Stop()
        {
            tokenSource.Cancel();
            await stoppable.Stop().ConfigureAwait(false);
        }

        public Task Subscribe(string messageType)
        {
            return Enqueue(messageType, ActionSubscribe, DateTime.UtcNow, Guid.NewGuid().ToString());
        }

        public Task Unsubscribe(string messageType)
        {
            return Enqueue(messageType, ActionUnsubscribe, DateTime.UtcNow, Guid.NewGuid().ToString());
        }

        const string ResubscriptionIdHeader = "NServiceBus.Bridge.ResubscriptionId";
        const string ResubscriptionTimestampHeader = "NServiceBus.Bridge.ResubscriptionTimestamp";
        const string ResubscriptionTypeHeader = "NServiceBus.Bridge.ResubscriptionType";
        const string ResubscriptionActionHeader = "NServiceBus.Bridge.ResubscriptionAction";
        const string ActionSubscribe = "Subscribe";
        const string ActionUnsubscribe = "Unsubscribe";
    }
}
