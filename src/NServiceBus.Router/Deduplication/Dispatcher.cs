using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Router;
using NServiceBus.Router.Deduplication;
using NServiceBus.Transport;

class Dispatcher : IModule
{
    OutboxPersistence persistence;
    SqlDeduplicationSettings settings;
    BlockingCollection<CapturedTransportOperation> operationsQueue;
    Task loopTask;
    CancellationTokenSource tokenSource;
    ILog logger = LogManager.GetLogger<Dispatcher>();
    Func<SqlConnection> connectionFactory;

    public Dispatcher(SqlDeduplicationSettings settings, OutboxPersistence persistence, Func<SqlConnection> connectionFactory)
    {
        this.settings = settings;
        this.persistence = persistence;
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
        loopTask = Task.Run(async () =>
        {
            var consumer = operationsQueue.GetConsumingEnumerable(tokenSource.Token);

            foreach (var operation in consumer)
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
                await persistence.MarkDispatched(operation, conn, trans).ConfigureAwait(false);

                var iface = settings.GetInterface(operation.Destination);

                var chains = rootContext.Interfaces.GetChainsFor(iface);
                var postroutingChain = chains.Get<PostroutingContext>();
                var dispatchContext = new PostroutingContext(operation.Operation, iface, rootContext);
                dispatchContext.Set(new TransportTransaction());
                await postroutingChain.Invoke(dispatchContext).ConfigureAwait(false);

                //Only commit the transaction if the dispatch succeeded.
                trans.Commit();
            }
        }
    }
}
