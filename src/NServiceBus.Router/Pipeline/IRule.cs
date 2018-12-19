namespace NServiceBus.Router
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a routing rule.
    /// </summary>
    /// <typeparam name="TInChain">Input context type.</typeparam>
    /// <typeparam name="TOutChain">Output context type.</typeparam>
    public interface IRule<in TInChain, out TOutChain> : IRule
        where TInChain : IRuleContext
        where TOutChain : IRuleContext
    {
        /// <summary>
        /// Executes the rule.
        /// </summary>
        /// <param name="context">Rule execution context.</param>
        /// <param name="next">Delegate to invoke the rest of the chain that this rule belongs to.</param>
        Task Invoke(TInChain context, Func<TOutChain, Task> next);
    }

    /// <summary>
    /// Marker interface for a routing rule
    /// </summary>
    public interface IRule
    {
    }
}
