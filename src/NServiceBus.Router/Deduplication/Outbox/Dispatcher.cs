using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Transport;

namespace NServiceBus.Router.Deduplication
{
    class Dispatcher : IModule
    {
        DeduplicationSettings settings;
        BlockingCollection<CapturedTransportOperation> operationsQueue;
        Task loopTask;
        CancellationTokenSource tokenSource;
        ILog logger = LogManager.GetLogger<Dispatcher>();
        Func<SqlConnection> connectionFactory;

        public Dispatcher(DeduplicationSettings settings, Func<SqlConnection> connectionFactory)
        {
            this.settings = settings;
            this.connectionFactory = connectionFactory;
            operationsQueue = new BlockingCollection<CapturedTransportOperation>(50);
        }

        public void Enqueue(CapturedTransportOperation operation)
        {
            operationsQueue.Add(operation);
        }

        public Task Start(RootContext rootContext)
        {
            tokenSource = new CancellationTokenSource();
            var throttle = new SemaphoreSlim(8);
            loopTask = Task.Run(async () =>
            {
                var consumer = operationsQueue.GetConsumingEnumerable(tokenSource.Token);

                foreach (var operation in consumer)
                {
                    await throttle.WaitAsync().ConfigureAwait(false);
                    var dispatchTask = Task.Run(async () =>
                    {
                        try
                        {
                            await Dispatch(operation, rootContext).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            //We can skip over messages because closing process will make sure all the messages are dispatched
                            //TODO: We might want some retries here.
                            logger.Error("Unhandled exception in the dispatcher", e);
                        }
                        finally
                        {
                            throttle.Release();
                        }
                    });
                    dispatchTask.Ignore();
                }
            });
            return Task.CompletedTask;
        }

        public async Task Stop()
        {
            try
            {
                //TODO: Do we need both?
                tokenSource.Cancel();
                operationsQueue.CompleteAdding();

                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }
        }

        async Task Dispatch(CapturedTransportOperation operation, RootContext rootContext)
        {
            using (var conn = connectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);

                using (var trans = conn.BeginTransaction())
                {
                    //Will block until the record insert transaction is completed.
                    await OutboxPersister.MarkAsDispatched(operation, conn, trans).ConfigureAwait(false);

                    var iface = settings.GetDestinationInterface(operation.Destination);

                    var chains = rootContext.Interfaces.GetChainsFor(iface);
                    var chain = chains.Get<AnycastContext>();
                    var dispatchContext = new OutboxDispatchContext(rootContext, iface);
                    var forwardContext = new AnycastContext(operation.Destination, operation.OutgoingMessage, DistributionStrategyScope.Send, dispatchContext);
                    dispatchContext.Set(new TransportTransaction());
                    await chain.Invoke(forwardContext).ConfigureAwait(false);

                    //Only commit the transaction if the dispatch succeeded.
                    trans.Commit();
                }
            }
        }
    }
}