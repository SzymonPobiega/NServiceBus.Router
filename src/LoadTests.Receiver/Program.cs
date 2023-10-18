namespace LoadTests.Sender
{
    using System;
    using System.Threading.Tasks;
    using Metrics;
    using Microsoft.Extensions.DependencyInjection;
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
            var config = new EndpointConfiguration("Receiver");
            config.UseSerialization<NewtonsoftJsonSerializer>();
            config.SendFailedMessagesTo("error");
            config.EnableInstallers();
            config.UsePersistence<NonDurablePersistence>();

            Metric.Config.WithReporting(r =>
            {
                r.WithCSVReports(".", TimeSpan.FromSeconds(5));
            });

            config.RegisterComponents(c => c.AddSingleton(new Statistics()));

            var connectionString = SettingsReader<string>.Read("SqlConnectionString", "data source=(local); initial catalog=loadtest; integrated security=true");

            var transport = new SqlServerTransport(connectionString);
            transport.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
            var routing = config.UseTransport(transport);

            routing.RouteToEndpoint(typeof(ProcessingReport), "Sender");

            endpointInstance = await Endpoint.Start(config);
            Console.WriteLine("Press <enter> to exit.");
            Console.ReadLine();

            await endpointInstance.Stop();
        }
    }
}