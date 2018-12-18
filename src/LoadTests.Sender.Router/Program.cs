using System;

namespace LoadTests.Sender.Router
{
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
            var epochSize = SettingsReader<int>.Read("EpochSize", 10000);

            var routerConfig = new RouterConfiguration("Sender.Router");
            routerConfig.AutoCreateQueues();
            var deduplicationConfig = routerConfig.ConfigureDeduplication();
#pragma warning disable 618
            deduplicationConfig.EnableInstaller(true);
#pragma warning restore 618

            var linkInterface = routerConfig.AddInterface<RabbitMQTransport>("Rabbit", t =>
            {
                t.ConnectionString(rabbitConnectionString);
                t.UseConventionalRoutingTopology();
            });

            var sqlInterface = routerConfig.AddInterface<SqlServerTransport>("SQL", t =>
            {
                t.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
            });

            sqlInterface.UseSubscriptionPersistence(new SqlSubscriptionStorage(() => new SqlConnection(sqlConnectionString), "SenderRouter", new SqlDialect.MsSqlServer(), null));
            sqlInterface.EnableDeduplication(linkInterface.Name, "Receiver.Router", () => new SqlConnection(sqlConnectionString), epochSize);

            var routingProtocol = routerConfig.UseStaticRoutingProtocol();
            routingProtocol.AddForwardRoute("SQL", "Rabbit", "Receiver.Router");

            var router = Router.Create(routerConfig);

            await router.Start();

            Console.WriteLine("Press <enter> to exit.");
            Console.ReadLine();

            await router.Stop();
        }
    }
}
