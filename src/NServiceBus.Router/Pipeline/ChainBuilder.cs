using System;
using System.Collections.Generic;
using System.Linq;

namespace NServiceBus.Router
{
    /// <summary>
    /// Builds rule chains.
    /// </summary>
    public class ChainBuilder
    {
        IRuleCreationContext context;
        Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules;

        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ChainBuilder(IRuleCreationContext context, Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules)
        {
            this.context = context;
            this.allRules = allRules;
        }

        /// <summary>
        /// Begins building a new rule chain.
        /// </summary>
        /// <typeparam name="T">Input type.</typeparam>
        public ChainBuilder<T, T> Begin<T>() where T : IRuleContext
        {
            var rules = allRules
                .Where(r => r.Key.GetInputContext() == typeof(T) && r.Key.GetOutputContext() == typeof(T))
                .Select(r => r.Value(context))
                .ToList();

            return new ChainBuilder<T, T>(allRules, rules, context);
        }
    }

    /// <summary>
    /// Builds rule chains.
    /// </summary>
    public class ChainBuilder<TInput, T>
        where TInput : IRuleContext
        where T : IRuleContext
    {
        Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules;
        List<IRule> rules;
        IRuleCreationContext context;

        internal ChainBuilder(Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules, List<IRule> rules, IRuleCreationContext context)
        {
            this.allRules = allRules;
            this.rules = rules;
            this.context = context;
        }

        /// <summary>
        /// Adds a secrion to the rule chain.
        /// </summary>
        /// <typeparam name="TNext">Section type.</typeparam>
        public ChainBuilder<TInput, TNext> AddSection<TNext>() where TNext : IRuleContext
        {
            var connector = allRules.Single(r => r.Key.GetInputContext() == typeof(T) && r.Key.GetOutputContext() == typeof(TNext));
            var chain = allRules.Where(r => r.Key.GetInputContext() == typeof(TNext) && r.Key.GetOutputContext() == typeof(TNext));

            rules.Add(connector.Value(context));
            rules.AddRange(chain.Select(r => r.Value(context)));

            return new ChainBuilder<TInput, TNext>(allRules, rules, context);
        }

        /// <summary>
        /// Finishes the creation of a rule chain.
        /// </summary>
        public IChain<TInput> Terminate()
        {
            var terminatorsFactories = allRules.Where(r => r.Key.GetInputContext() == typeof(T) && r.Key.GetOutputContext() == typeof(ChainTerminator<T>.ITerminatingContext));

            var terminators = terminatorsFactories.Select(f => f.Value(context)).Cast<IRule<T, ChainTerminator<T>.ITerminatingContext>>().ToList();
            if (!terminators.Any())
            {
                throw new Exception($"A chain has to have at least one terminator: {typeof(T).Name}.");
            }
            rules.Add(new TerminatorInvocationRule<T>(terminators));

            return new Chain<TInput>(rules.ToArray().CreatePipelineExecutionFuncFor<TInput>());
        }
    }
}