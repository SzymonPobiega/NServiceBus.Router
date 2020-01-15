using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router.Migrator;

class Program
{
    static async Task Main()
    {
        Console.Title = "ShippingGateway";
        var endpointConfiguration = new EndpointConfiguration("Migrator.ShippingGateway");
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
        endpointConfiguration.UseTransport<MsmqTransport>();
    }

    static void ConfigureTransportMigrationMode(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.EnableTransportMigration<MsmqTransport, SqlServerTransport>(
            msmq => { },
            sql =>
            {
                sql.ConnectionString(ConnectionStrings.Transport);
            });
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