using System.Threading.Tasks;

namespace NServiceBus.Router
{
    /// <summary>
    /// An instance of a router
    /// </summary>
    public interface IRouter
    {
        /// <summary>
        /// Starts the router.
        /// </summary>
        Task Start();

        /// <summary>
        /// Stops the router.
        /// </summary>
        Task Stop();
    }
}