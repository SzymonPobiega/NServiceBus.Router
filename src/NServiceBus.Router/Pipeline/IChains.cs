namespace NServiceBus.Router
{
    /// <summary>
    /// Allows accessing all chains associated with a given interface.
    /// </summary>
    public interface IChains
    {
        /// <summary>
        /// Returns a chain for a given rule context type <typeparamref name="T"/>.
        /// </summary>
        IChain<T> Get<T>() where T : IRuleContext;
    }
}