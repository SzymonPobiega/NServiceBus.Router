using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router.Migrator;

class Program
{
    static async Task Main()
    {
        Console.Title = "Sales";
        var endpointConfiguration = new EndpointConfiguration("Migrator.Sales");
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
        routing.RegisterPublisher(typeof(ShipmentScheduled), "Migrator.Shipping");
    }

    static void ConfigureTransportMigrationMode(EndpointConfiguration endpointConfiguration)
    {
        var routing = endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
            });
        routing.RegisterPublisher(typeof(ShipmentScheduled), "Migrator.Shipping");
    }

    static void ConfigureTransportDrainMode(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
            });
    }

    static void ConfigureTransportSql(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.UseTransport<SqlServerTransport>()
            .ConnectionString(ConnectionStrings.Transport);
    }
}