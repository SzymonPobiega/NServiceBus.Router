using System;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        Console.Title = "Sales";
        var endpointConfiguration = new EndpointConfiguration("Migrator.Sales");
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.EnableInstallers();

        //var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        //transport.ConnectionString(ConnectionStrings.Transport);
        //SqlHelper.EnsureDatabaseExists(ConnectionStrings.Transport);

        var transport = endpointConfiguration.UseTransport<MsmqTransport>();
        transport.Routing().RegisterPublisher(typeof(ShipmentScheduled), "Migrator.Shipping");

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}