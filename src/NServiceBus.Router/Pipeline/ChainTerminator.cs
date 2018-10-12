namespace NServiceBus.Router
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Marks end of a rule chain.
    /// </summary>
    /// <typeparam name="T">The rule context type to terminate.</typeparam>
    public abstract class ChainTerminator<T> : IRule<T, ChainTerminator<T>.ITerminatingContext>, IChainTerminator where T : IRuleContext
    {
        /// <summary>
        /// This method will be the final one to be called before the chain starts to traverse back up the "stack".
        /// </summary>
        /// <param name="context">The current context.</param>
        protected abstract Task Terminate(T context);

        /// <summary>
        /// Invokes the terminate method.
        /// </summary>
        /// <param name="context">Context object.</param>
        /// <param name="next">Ignored since there by definition is no next rule to call.</param>
        public Task Invoke(T context, Func<ITerminatingContext, Task> next)
        {
            return Terminate(context);
        }

        /// <summary>
        /// A well-known context that terminates the pipeline.
        /// </summary>
        public interface ITerminatingContext : IRuleContext
        {
        }
    }

    interface IChainTerminator
    {
    }
}