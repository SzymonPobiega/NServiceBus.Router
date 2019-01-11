using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus.Router;

class InterfaceChains : IInterfaceChains
{
    Dictionary<string, IChains> interfaceMap = new Dictionary<string, IChains>();
    List<RuleRegistration> rules = new List<RuleRegistration>();
    List<Action<ChainBuilder, Dictionary<Type, object>>> chainRegistrations = new List<Action<ChainBuilder, Dictionary<Type, object>>>();

    public void AddChain<TInput>(Func<ChainBuilder, IChain<TInput>> chainDefinition)
        where TInput : IRuleContext
    {
        chainRegistrations.Add((builder, registrations) =>
        {
            registrations[typeof(TInput)] = chainDefinition(builder);
        });
    }

    public void InitializeInterface(string interfaceName, IRuleCreationContext context)
    {
        var applicableRules = rules.Where(r => r.Condition(context)).ToList();

        var chains = new Chains(new ChainBuilder(context, applicableRules), chainRegistrations);
        interfaceMap[interfaceName] = chains;
    }

    public void AddRule<T>(Func<IRuleCreationContext, T> ruleConstructor, Func<IRuleCreationContext, bool> condition = null)
        where T : IRule
    {
        rules.Add(new RuleRegistration(typeof(T),  c => ruleConstructor(c), condition ?? (c => true)));
    }

    public IChains GetChainsFor(string @interface)
    {
        if (interfaceMap.TryGetValue(@interface, out var chains))
        {
            return chains;
        }
        throw new UnforwardableMessageException($"Interface '{@interface}' has not been configured.");
    }
}