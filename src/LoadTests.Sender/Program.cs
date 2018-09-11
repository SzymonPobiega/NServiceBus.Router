namespace LoadTests.Sender
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;

    class Program
    {
        static IEndpointInstance endpointInstance;

        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var config = new EndpointConfiguration("Sender");
            config.UseSerialization<NewtonsoftSerializer>();
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();
            config.UsePersistence<InMemoryPersistence>();

            var useRouter = SettingsReader<bool>.Read("UseRouter", true);
            var connectionString = SettingsReader<string>.Read("SqlConnectionString", "data source=(local); initial catalog=loadtest; integrated security=true");
            var bodySize = SettingsReader<int>.Read("BodySize");
            var maxConcurrency = SettingsReader<int>.Read("MaxConcurrency", 32);

            config.RegisterComponents(c => c.RegisterSingleton(new LoadGenerator((info, token) => GenerateMessages(bodySize, info, token, maxConcurrency), 10000, 20000)));

            var senderTransport = config.UseTransport<SqlServerTransport>();
            senderTransport.ConnectionString(connectionString);
            senderTransport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

            if (useRouter)
            {
                var senderRouterConnector = senderTransport.Routing().ConnectToRouter("Sender.Router");
                senderRouterConnector.RouteToEndpoint(typeof(MyMessage), "Receiver");
            }
            else
            {
                senderTransport.Routing().RouteToEndpoint(typeof(MyMessage), "Receiver");
            }

            endpointInstance = await Endpoint.Start(config);
            Console.WriteLine("Press <enter> to exit.");
            Console.ReadLine();

            await endpointInstance.Stop();
        }

        static async Task GenerateMessages(int bodySize, QueueInfo queueInfo, CancellationToken token, int maxConcurrency)
        {
            var throttle = new SemaphoreSlim(maxConcurrency);
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
                        var auditMessage = new MyMessage
                        {
                            Data = new byte[bodySize]
                        };
                        random.NextBytes(auditMessage.Data);

                        await endpointInstance.Send(auditMessage).ConfigureAwait(false);
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


}