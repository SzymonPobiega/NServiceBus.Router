using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus.Router;

class ChainBuilder
{
    IRuleCreationContext context;
    Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules;

    public ChainBuilder(IRuleCreationContext context, Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules)
    {
        this.context = context;
        this.allRules = allRules;
    }

    public ChainBuilder<T, T> Begin<T>() where T : IRuleContext
    {
        var rules = allRules
            .Where(r => r.Key.GetInputContext() == typeof(T) && r.Key.GetOutputContext() == typeof(T))
            .Select(r => r.Value(context))
            .ToList();

        return new ChainBuilder<T, T>(allRules, rules, context);
    }
}

class ChainBuilder<TInput, T> 
    where TInput : IRuleContext
    where T : IRuleContext
{
    Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules;
    List<IRule> rules;
    IRuleCreationContext context;

    public ChainBuilder(Dictionary<Type, Func<IRuleCreationContext, IRule>> allRules, List<IRule> rules, IRuleCreationContext context)
    {
        this.allRules = allRules;
        this.rules = rules;
        this.context = context;
    }

    public ChainBuilder<TInput, TNext> AddSection<TNext>() where TNext : IRuleContext
    {
        var connector = allRules.Single(r => r.Key.GetInputContext() == typeof(T) && r.Key.GetOutputContext() == typeof(TNext));
        var chain = allRules.Where(r => r.Key.GetInputContext() == typeof(TNext) && r.Key.GetOutputContext() == typeof(TNext));

        rules.Add(connector.Value(context));
        rules.AddRange(chain.Select(r => r.Value(context)));

        return new ChainBuilder<TInput, TNext>(allRules, rules, context);
    }

    public IChain<TInput> Terminate()
    {
        var terminator = allRules.Single(r => r.Key.GetInputContext() == typeof(T) && r.Key.GetOutputContext() == typeof(ChainTerminator<T>.ITerminatingContext));
        rules.Add(terminator.Value(context));

        return new Chain<TInput>(rules.ToArray().CreatePipelineExecutionFuncFor<TInput>());
    }
}