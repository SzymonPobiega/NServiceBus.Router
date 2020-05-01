using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace NServiceBus.Router.Hosting
{
    /// <summary>
    /// IHostedService for NServiceBusRouter
    /// </summary>
    public class NServiceBusRouterHostedService : IHostedService
    {
        private readonly IRouter _router;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="router"></param>
        public NServiceBusRouterHostedService(IRouter router)
        {
            _router = router;
        }

        /// <summary>
        /// StartAsync
        /// </summary>
        /// <param name="cancellationToken"></param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _router.Start().ConfigureAwait(false);
        }

        /// <summary>
        /// StopAsync
        /// </summary>
        /// <param name="cancellationToken"></param>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _router.Stop().ConfigureAwait(false);
        }
    }
}