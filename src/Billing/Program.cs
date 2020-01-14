using System;
using System.Threading.Tasks;
using NServiceBus;

class Program
{
    static async Task Main()
    {
        Console.Title = "Billing";
        var endpointConfiguration = new EndpointConfiguration("Migrator.Billing");
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.EnableInstallers();

        //var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        //transport.ConnectionString(ConnectionStrings.Transport);

        var transport = endpointConfiguration.UseTransport<MsmqTransport>();
        transport.Routing().RouteToEndpoint(typeof(AuthorizeTransaction), "Migrator.PaymentGateway");
        transport.Routing().RegisterPublisher(typeof(OrderAccepted), "Migrator.Sales");

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }
}