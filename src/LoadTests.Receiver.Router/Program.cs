namespace LoadTests.Sender.Router
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Router;

    class Program
    {
        static void Main(string[] args)
        {
            Start().GetAwaiter().GetResult();
        }

        static async Task Start()
        {
            var sqlConnectionString = SettingsReader<string>.Read("SqlConnectionString", "data source=(local); initial catalog=loadtest; integrated security=true");
            var rabbitConnectionString = SettingsReader<string>.Read("RabbitConnectionString", "host=localhost");
            var deduplication = SettingsReader<bool>.Read("Deduplicate");
            var epochSize = SettingsReader<int>.Read("EpochSize", 10000);

            var routerConfig = new RouterConfiguration("Receiver.Router");
            routerConfig.AutoCreateQueues();
            routerConfig.AddInterface<SqlServerTransport>("SQL", t =>
                {
                    t.ConnectionString(sqlConnectionString);
                    t.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
                })
                .UseSubscriptionPersistence(new SqlSubscriptionStorage(() => new SqlConnection(sqlConnectionString), "ReceiverRouter", new SqlDialect.MsSqlServer(), null));

            routerConfig.AddInterface<RabbitMQTransport>("Rabbit", t =>
            {
                t.ConnectionString(rabbitConnectionString);
                t.UseConventionalRoutingTopology();
            });

            var routingProtocol = routerConfig.UseStaticRoutingProtocol();
            routingProtocol.AddForwardRoute("Rabbit", "SQL");

            if (deduplication)
            {
                routerConfig.EnableDeduplication(d =>
                {
#pragma warning disable 618
                    d.EnableInstaller(true);
#pragma warning restore 618
                    d.EpochSize(epochSize);
                    d.ConnectionFactory(() => new SqlConnection(sqlConnectionString));
                    d.AddIncomingLink("Rabbit", "Sender.Router");
                });
            }

            var router = Router.Create(routerConfig);

            await router.Start();

            Console.WriteLine("Press <enter> to exit.");
            Console.ReadLine();

            await router.Stop();
        }
    }
}
