namespace NServiceBus.Router
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a single routing chain of a given type <typeparamref name="T"/> associated with a single interface.
    /// </summary>
    public interface IChain<in T> where T : IRuleContext
    {
        /// <summary>
        /// Begins execution of the chain.
        /// </summary>
        Task Invoke(T context);
    }
}