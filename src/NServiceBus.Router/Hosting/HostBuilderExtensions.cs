using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace NServiceBus.Router.Hosting
{
    /// <summary>
    /// HostBuilder extensions
    /// </summary>
    public static class HostBuilderExtensions
    {
        /// <summary>
        /// Configures the host to start an NServiceBusRouter.
        /// </summary>
        public static IHostBuilder UseNServiceBusRouter(this IHostBuilder hostBuilder, Func<HostBuilderContext, RouterConfiguration> routerConfigurationBuilder)
        {
            hostBuilder.ConfigureServices((ctx, serviceCollection) =>
            {
                var routerConfiguration = routerConfigurationBuilder(ctx);

                serviceCollection.AddSingleton(_ => Router.Create(routerConfiguration));
                serviceCollection.AddSingleton<IHostedService, NServiceBusRouterHostedService>();
            });

            return hostBuilder;
        }
    }
}