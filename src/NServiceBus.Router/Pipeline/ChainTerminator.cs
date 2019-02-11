namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Logging;

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
        protected abstract Task<bool> Terminate(T context);

        /// <summary>
        /// Invokes the terminate method.
        /// </summary>
        /// <param name="context">Context object.</param>
        /// <param name="next">Ignored since there by definition is no next rule to call.</param>
        public async Task Invoke(T context, Func<ITerminatingContext, Task> next)
        {
            var handled = await Terminate(context).ConfigureAwait(false);
            if (handled)
            {
                await next(null);
            }
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

    class TerminatorInvocationRule<T> : IRule<T, ChainTerminator<T>.ITerminatingContext>, IChainTerminator
        where T : IRuleContext
    {
        static ILog log = LogManager.GetLogger<T>();

        Dictionary<string, IRule<T, ChainTerminator<T>.ITerminatingContext>> terminators;

        public TerminatorInvocationRule(List<IRule<T, ChainTerminator<T>.ITerminatingContext>> terminators)
        {
            this.terminators = terminators.ToDictionary(x => x.GetType().Name, x => x);
        }

        public async Task Invoke(T context, Func<ChainTerminator<T>.ITerminatingContext, Task> next)
        {
            var invoked = new List<string>();

            foreach (var terminator in terminators)
            {
                await terminator.Value.Invoke(context, terminatingContext =>
                {
                    invoked.Add(terminator.Key);
                    return Task.CompletedTask;
                }).ConfigureAwait(false);

            }

            if (invoked.Count < 1)
            {
                throw new UnforwardableMessageException($"No terminator handled the message in the {typeof(T).Name} chain. This might indicate a configuration problem. If the message should be dropped, register a chain terminator that explicitly marks that type of messages as handled.");
            }
            if (invoked.Count > 1)
            {
                log.Warn($"Multiple terminators ({string.Join(",", invoked)}) handled the message. This might indicate a configuration problem.");
            }

        }
    }
}