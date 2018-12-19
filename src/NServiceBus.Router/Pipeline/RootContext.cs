using NServiceBus.Extensibility;

namespace NServiceBus.Router
{
    /// <summary>
    /// Root context for the router.
    /// </summary>
    public class RootContext : ContextBag, IRuleContext
    {
        internal RootContext(IInterfaceChains interfaces)
        {
            Set(interfaces);
        }

        /// <summary>
        /// Router's interfaces.
        /// </summary>
        public IInterfaceChains Interfaces => Get<IInterfaceChains>();

        /// <summary>
        /// Allows extending the rule context by adding arbitrary values.
        /// </summary>
        public ContextBag Extensions => this;
    }
}

