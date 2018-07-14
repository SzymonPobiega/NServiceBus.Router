using System;
using NServiceBus.Router;

class RuleRegistration
{
    public Type Type { get; }
    public Func<IRuleCreationContext, IRule> Constructor { get; }

    public Func<IRuleCreationContext, bool> Condition { get; }

    public RuleRegistration(Type type, Func<IRuleCreationContext, IRule> constructor, Func<IRuleCreationContext, bool> condition)
    {
        Type = type;
        Constructor = constructor;
        Condition = condition;
    }
}