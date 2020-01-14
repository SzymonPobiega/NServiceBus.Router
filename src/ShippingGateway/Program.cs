using System;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        Console.Title = "ShippingGateway";
        var endpointConfiguration = new EndpointConfiguration("Migrator.ShippingGateway");
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.EnableInstallers();

        //var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        //transport.ConnectionString(ConnectionStrings.Transport);

        endpointConfiguration.UseTransport<MsmqTransport>();

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}