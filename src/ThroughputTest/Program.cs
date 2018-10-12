using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Metrics;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Support;

class Program
{
    const string ConnectionString = "data source = (local); initial catalog=test2; integrated security=true";
    static IEndpointInstance sender;

    static void Main(string[] args)
    {
        Start().GetAwaiter().GetResult();
    }

    static async Task Start()
    {
        Metric.Config.WithReporting(r =>
        {
            r.WithCSVReports(".", TimeSpan.FromSeconds(5));
        });

        var senderConfig = new EndpointConfiguration("Sender");
        senderConfig.UseSerialization<NewtonsoftSerializer>();
        senderConfig.SendFailedMessagesTo("error");
        senderConfig.EnableInstallers();
        senderConfig.UsePersistence<InMemoryPersistence>();

        senderConfig.RegisterComponents(c => c.RegisterSingleton(new LoadGenerator(GenerateMessages, 5000, 10000)));

        var senderTransport = senderConfig.UseTransport<SqlServerTransport>();
        senderTransport.ConnectionString(ConnectionString);
        senderTransport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

        var senderRouterConnector = senderTransport.Routing().ConnectToRouter("Router");
        senderRouterConnector.RouteToEndpoint(typeof(MyMessage), "Receiver");

        senderTransport.Routing().RouteToEndpoint(typeof(MyMessage), "Receiver");

        var routerConfig = new RouterConfiguration("Router");
        routerConfig.AutoCreateQueues();
        routerConfig.AddInterface<SqlServerTransport>("SQL", t =>
        {
            t.ConnectionString(ConnectionString);
            t.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
        });
        routerConfig.AddInterface<RabbitMQTransport>("Rabbit", t =>
        {
            t.ConnectionString("host=localhost");
            t.UseConventionalRoutingTopology();
        });

        var routingProtocol = routerConfig.UseStaticRoutingProtocol();
        routingProtocol.AddForwardRoute("SQL", "Rabbit");
        routingProtocol.AddForwardRoute("Rabbit", "SQL");

        routerConfig.EnableDeduplication(d =>
        {
            d.EpochSize(1000);
            d.ConnectionFactory(() => new SqlConnection(ConnectionString));
            d.AddOutgoingLink("Rabbit", "Receiver");
        });

        var receiverConfig = new EndpointConfiguration("Receiver");
        receiverConfig.UseSerialization<NewtonsoftSerializer>();
        receiverConfig.SendFailedMessagesTo("error");
        receiverConfig.EnableInstallers();
        receiverConfig.UsePersistence<InMemoryPersistence>();
        receiverConfig.EnableFeature<ReporterFeature>();

        //var receiverTransport = receiverConfig.UseTransport<RabbitMQTransport>();
        //receiverTransport.ConnectionString("host=localhost");
        //receiverTransport.UseConventionalRoutingTopology();

        var receiverTransport = receiverConfig.UseTransport<SqlServerTransport>();
        receiverTransport.ConnectionString(ConnectionString);
        receiverTransport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

        //var receiverRouterConnector = receiverTransport.Routing().ConnectToRouter("Router");
        //receiverRouterConnector.RouteToEndpoint(typeof(ProcessingReport), "Sender");

        receiverTransport.Routing().RouteToEndpoint(typeof(ProcessingReport), "Sender");

        //var router = Router.Create(routerConfig);
        //await router.Start();

        sender = await Endpoint.Start(senderConfig);

        var receiver = await Endpoint.Start(receiverConfig);

        Console.WriteLine("Press <enter> to exit");
        Console.ReadLine();

        await sender.Stop();
        await receiver.Stop();
        //await router.Stop();
    }

    static async Task GenerateMessages(QueueInfo queueInfo, CancellationToken token)
    {
        var throttle = new SemaphoreSlim(32);
        var bodySize = 0;

        var random = new Random();

        var sendMeter = Metric.Meter("Sent", Unit.Custom("messages"));

        while (!token.IsCancellationRequested)
        {
            await throttle.WaitAsync(token).ConfigureAwait(false);
            // We don't need to wait for this task
            // ReSharper disable once UnusedVariable
            var sendTask = Task.Run(async () =>
            {
                try
                {
                    var ops = new SendOptions();

                    ops.SetHeader(Headers.HostId, "MyHost");
                    ops.SetHeader(Headers.HostDisplayName, "Load Generator");

                    ops.SetHeader(Headers.ProcessingMachine, RuntimeEnvironment.MachineName);
                    ops.SetHeader(Headers.ProcessingEndpoint, "LoadGenerator");

                    var now = DateTime.UtcNow;
                    ops.SetHeader(Headers.ProcessingStarted, DateTimeExtensions.ToWireFormattedString(now));
                    ops.SetHeader(Headers.ProcessingEnded, DateTimeExtensions.ToWireFormattedString(now));


                    var auditMessage = new MyMessage();
                    auditMessage.Data = new byte[bodySize];
                    random.NextBytes(auditMessage.Data);

                    await sender.Send(auditMessage, ops).ConfigureAwait(false);
                    queueInfo.Sent();
                    sendMeter.Mark();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    throttle.Release();
                }
            }, token);
        }
    }
}