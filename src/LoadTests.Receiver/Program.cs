namespace LoadTests.Sender
{
    using System;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using NServiceBus.Transport.SQLServer;

    class Program
    {
        static IEndpointInstance endpointInstance;

        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var config = new EndpointConfiguration("Receiver");
            config.UseSerialization<NewtonsoftSerializer>();
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();
            config.UsePersistence<InMemoryPersistence>();

            Metric.Config.WithReporting(r =>
            {
                r.WithCSVReports(".", TimeSpan.FromSeconds(5));
            });

            config.RegisterComponents(c => c.RegisterSingleton(new Statistics()));

            var connectionString = SettingsReader<string>.Read("SqlConnectionString", "data source=(local); initial catalog=loadtest; integrated security=true");

            var senderTransport = config.UseTransport<SqlServerTransport>();
            senderTransport.UseNativeDelayedDelivery().DisableTimeoutManagerCompatibility();
            senderTransport.ConnectionString(connectionString);
            senderTransport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

            senderTransport.Routing().RouteToEndpoint(typeof(ProcessingReport), "Sender");

            endpointInstance = await Endpoint.Start(config);
            Console.WriteLine("Press <enter> to exit.");
            Console.ReadLine();

            await endpointInstance.Stop();
        }
    }
}