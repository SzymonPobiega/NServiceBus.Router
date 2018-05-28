namespace NServiceBus.Router
{
    using System.Threading.Tasks;

    /// <summary>
    /// Encapsulates a protocol for building the routing table.
    /// </summary>
    public interface IRoutingProtocol
    {
        /// <summary>
        /// Starts the protocol.
        /// </summary>
        /// <returns></returns>
        Task Start(RouterMetadata metadata);

        /// <summary>
        /// Stops the protocol.
        /// </summary>
        Task Stop();

        /// <summary>
        /// Gets the current route table.
        /// </summary>
        RouteTable RouteTable { get; }
    }
}