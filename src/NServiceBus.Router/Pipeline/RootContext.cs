using NServiceBus.Extensibility;

namespace NServiceBus.Router
{
    using System.Threading;

    /// <summary>
    /// Root context for the router.
    /// </summary>
    public class RootContext : ContextBag, IRuleContext
    {
        internal RootContext(IInterfaceChains interfaces, string routerName, CancellationToken cancellationToken)
        {
            RouterName = routerName;
            CancellationToken = cancellationToken;
            Set(interfaces);
        }

        /// <summary>
        /// The logical name of the router
        /// </summary>
        public string RouterName { get; }

        /// <summary>
        /// Router's interfaces.
        /// </summary>
        public IInterfaceChains Interfaces => Get<IInterfaceChains>();

        /// <summary>
        /// Allows extending the rule context by adding arbitrary values.
        /// </summary>
        public ContextBag Extensions => this;

        /// <summary>
        /// Cancellation token for graceful shutdown
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}

