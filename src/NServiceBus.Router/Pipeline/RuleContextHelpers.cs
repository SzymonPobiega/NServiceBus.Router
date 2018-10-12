using System;
using System.Linq;
using NServiceBus.Router;

static class RuleContextHelpers
{
    public static Type GetRuleInterface(this Type behaviorType)
    {
        return behaviorType.GetInterfaces()
            .First(x => x.IsGenericType && x.GetGenericTypeDefinition() == RuleInterfaceType);
    }

    public static Type GetOutputContext(this Type behaviorType)
    {
        var behaviorInterface = GetRuleInterface(behaviorType);
        return behaviorInterface.GetGenericArguments()[1];
    }

    public static Type GetInputContext(this Type behaviorType)
    {
        var behaviorInterface = GetRuleInterface(behaviorType);
        return behaviorInterface.GetGenericArguments()[0];
    }

    static Type RuleInterfaceType = typeof(IRule<,>);
}
