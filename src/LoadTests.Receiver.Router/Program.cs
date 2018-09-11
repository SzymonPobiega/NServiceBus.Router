using System;

namespace LoadTests.Sender.Router
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Router;
    using NServiceBus.Router.Deduplication;

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
                routerConfig.EnableSqlDeduplication(d =>
                {
                    d.EpochSize(epochSize);
                    d.ConnectionFactory(() => new SqlConnection(sqlConnectionString));
                    d.DecuplicateIncomingMessagesBasedOnTotalOrder("Rabbit", "Sender.Router");
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
