using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Router.Deduplication;

class Installer : IModule
{
    SqlDeduplicationSettings settings;
    OutboxPersistence persistence;
    Func<SqlConnection> connectionFactory;

    public Installer(SqlDeduplicationSettings settings, OutboxPersistence persistence, Func<SqlConnection> connectionFactory)
    {
        this.settings = settings;
        this.persistence = persistence;
        this.connectionFactory = connectionFactory;
    }

    public async Task Start(RootContext rootContext)
    {
        using (var conn = connectionFactory())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            using (var trans = conn.BeginTransaction())
            {
                foreach (var destination in settings.GetAllDestinations())
                {
                    await persistence.Install(destination, conn, trans).ConfigureAwait(false);
                }
                trans.Commit();
            }
        }
    }

    public Task Stop()
    {
        return Task.CompletedTask;
    }
}