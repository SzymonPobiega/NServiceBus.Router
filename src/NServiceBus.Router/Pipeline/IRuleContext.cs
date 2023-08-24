namespace NServiceBus.Router
{
    using System.Threading;
    using Extensibility;

    /// <summary>
    /// Defines the context for the routing rule.
    /// </summary>
    public interface IRuleContext
    {
        /// <summary>
        /// Allows extending the rule context by adding arbitrary values.
        /// </summary>
        ContextBag Extensions { get; }

        /// <summary>
        /// Context passed during message processing
        /// </summary>
        CancellationToken CancellationToken { get; }
    }
}