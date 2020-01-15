using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router.Migrator;

class Program
{
    static async Task Main()
    {
        Console.Title = "Shipping";
        var endpointConfiguration = new EndpointConfiguration("Migrator.Shipping");
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.EnableInstallers();

        //ConfigureTransportMsmq(endpointConfiguration);
        ConfigureTransportMigrationMode(endpointConfiguration);
        //ConfigureTransportDrainMode(endpointConfiguration);

        var endpointInstance = await Endpoint.Start(endpointConfiguration)
            .ConfigureAwait(false);
        Console.WriteLine("Press any key to exit");
        Console.ReadKey();
        await endpointInstance.Stop()
            .ConfigureAwait(false);
    }

    static void ConfigureTransportMsmq(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.UseTransport<MsmqTransport>().Routing();
        routing.RegisterPublisher(typeof(PaymentCompleted), "Migrator.Billing");
        routing.RouteToEndpoint(typeof(ScheduleShipment), "Migrator.ShippingGateway");
    }

    static void ConfigureTransportMigrationMode(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
            });
        routing.RegisterPublisher(typeof(PaymentCompleted), "Migrator.Billing");
        routing.RouteToEndpoint(typeof(ScheduleShipment), "Migrator.ShippingGateway");
    }

    static void ConfigureTransportDrainMode(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
                sql.Routing().RouteToEndpoint(typeof(ScheduleShipment), "Migrator.ShippingGateway");
            });
    }

    static void ConfigureTransportSql(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.UseTransport<SqlServerTransport>()
            .ConnectionString(ConnectionStrings.Transport).Routing();

        routing.RouteToEndpoint(typeof(ScheduleShipment), "Migrator.ShippingGateway");
    }
}