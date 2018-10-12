using System;
using System.Collections.Generic;
using System.Linq;
using NServiceBus.Raw;
using NServiceBus.Router;

class InterfaceChains : IInterfaceChains
{
    Dictionary<string, IChains> interfaceMap = new Dictionary<string, IChains>();
    List<RuleRegistration> rules = new List<RuleRegistration>();

    public IChain<RawContext> RegisterInterface(IRuleCreationContext context, string interfaceName, IRawEndpoint rawEndpoint)
    {
        var applicableRules = rules.Where(r => r.Condition(context))
            .ToDictionary(r => r.Type, r => r.Constructor);

        var chains = new Chains(new ChainBuilder(context, applicableRules));
        interfaceMap[interfaceName] = chains;
        return chains.Get<RawContext>();
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