using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router.Migrator;

class Program
{
    static async Task Main()
    {
        Console.Title = "Billing";
        var endpointConfiguration = new EndpointConfiguration("Migrator.Billing");
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
        routing.RegisterPublisher(typeof(OrderAccepted), "Migrator.Sales");
        routing.RouteToEndpoint(typeof(AuthorizeTransaction), "Migrator.PaymentGateway");
    }

    static void ConfigureTransportMigrationMode(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
            });
        routing.RegisterPublisher(typeof(OrderAccepted), "Migrator.Sales");
        routing.RouteToEndpoint(typeof(AuthorizeTransaction), "Migrator.PaymentGateway");
    }

    static void ConfigureTransportDrainMode(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
                sql.Routing().RouteToEndpoint(typeof(AuthorizeTransaction), "Migrator.PaymentGateway");
            });
    }

    static void ConfigureTransportSql(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.UseTransport<SqlServerTransport>()
            .ConnectionString(ConnectionStrings.Transport).Routing();
        routing.RouteToEndpoint(typeof(AuthorizeTransaction), "Migrator.PaymentGateway");
    }
}