using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router.Migrator;

class Program
{
    static async Task Main()
    {
        Console.Title = "Client";
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVXYZ";
        var random = new Random();

        var endpointConfiguration = new EndpointConfiguration("Migrator.Client");
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.EnableInstallers();

        //ConfigureTransportMsmq(endpointConfiguration);
        ConfigureTransportMigrationMode(endpointConfiguration);
        //ConfigureTransportDrainMode(endpointConfiguration);

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press enter to send a message");
        Console.WriteLine("Press any key to exit");

        while (true)
        {
            var key = Console.ReadKey();
            Console.WriteLine();

            if (key.Key != ConsoleKey.Enter)
            {
                break;
            }
            var orderId = new string(Enumerable.Range(0, 4).Select(x => letters[random.Next(letters.Length)]).ToArray());
            Console.WriteLine($"Placing order {orderId}");
            var message = new PlaceOrder
            {
                OrderId = orderId,
            };
            await endpointInstance.Send(message)
                .ConfigureAwait(false);
        }
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }

    static void ConfigureTransportMsmq(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.UseTransport<MsmqTransport>().Routing();
        routing.RouteToEndpoint(typeof(PlaceOrder), "Migrator.Sales");
        routing.RegisterPublisher(typeof(OrderCompleted), "Migrator.Sales");
    }

    static void ConfigureTransportMigrationMode(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
            });
        routing.RouteToEndpoint(typeof(PlaceOrder), "Migrator.Sales");
        routing.RegisterPublisher(typeof(OrderCompleted), "Migrator.Sales");
    }

    static void ConfigureTransportDrainMode(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
                sql.Routing().RouteToEndpoint(typeof(PlaceOrder), "Migrator.Sales");
            });
    }

    static void ConfigureTransportSql(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.UseTransport<SqlServerTransport>()
            .ConnectionString(ConnectionStrings.Transport).Routing();
        routing.RouteToEndpoint(typeof(PlaceOrder), "Migrator.Sales");
    }
}