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
        List<RuleRegistration> allRules;

        internal ChainBuilder(IRuleCreationContext context, List<RuleRegistration> allRules)
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
                .Where(r => r.Type.GetInputContext() == typeof(T) && r.Type.GetOutputContext() == typeof(T))
                .Select(r => r.Constructor(context))
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
        List<RuleRegistration> allRules;
        List<IRule> rules;
        IRuleCreationContext context;

        internal ChainBuilder(List<RuleRegistration> allRules, List<IRule> rules, IRuleCreationContext context)
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
            var connector = allRules.Single(r => r.Type.GetInputContext() == typeof(T) && r.Type.GetOutputContext() == typeof(TNext));
            var chain = allRules.Where(r => r.Type.GetInputContext() == typeof(TNext) && r.Type.GetOutputContext() == typeof(TNext));

            rules.Add(connector.Constructor(context));
            rules.AddRange(chain.Select(r => r.Constructor(context)));

            return new ChainBuilder<TInput, TNext>(allRules, rules, context);
        }

        /// <summary>
        /// Finishes the creation of a rule chain.
        /// </summary>
        public IChain<TInput> Terminate()
        {
            var terminatorsFactories = allRules.Where(r => r.Type.GetInputContext() == typeof(T) && r.Type.GetOutputContext() == typeof(ChainTerminator<T>.ITerminatingContext));

            var terminators = terminatorsFactories.Select(f => f.Constructor(context)).Cast<IRule<T, ChainTerminator<T>.ITerminatingContext>>().ToList();
            if (!terminators.Any())
            {
                throw new Exception($"A chain has to have at least one terminator: {typeof(T).Name}.");
            }
            rules.Add(new TerminatorInvocationRule<T>(terminators));

            return new Chain<TInput>(rules.ToArray().CreatePipelineExecutionFuncFor<TInput>());
        }
    }
}