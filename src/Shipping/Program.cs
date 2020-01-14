using System;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        Console.Title = "Shipping";
        var endpointConfiguration = new EndpointConfiguration("Migrator.Shipping");
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.EnableInstallers();

        //var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        //transport.ConnectionString(ConnectionStrings.Transport);

        var transport = endpointConfiguration.UseTransport<MsmqTransport>();
        transport.Routing().RegisterPublisher(typeof(PaymentCompleted), "Migrator.Billing");
        transport.Routing().RouteToEndpoint(typeof(ScheduleShipment), "Migrator.ShippingGateway");

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}