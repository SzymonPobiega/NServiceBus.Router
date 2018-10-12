using System;
using System.Collections.Generic;
using NServiceBus.Router;

class Chains : IChains
{
    Dictionary<Type, object> chains = new Dictionary<Type, object>();

    public Chains(ChainBuilder builder, List<Action<ChainBuilder, Dictionary<Type, object>>> chainRegistrations)
    {
        foreach (var chainRegistration in chainRegistrations)
        {
            chainRegistration(builder, chains);
        }
    }

    public IChain<T> Get<T>() where T : IRuleContext
    {
        return (IChain<T>)chains[typeof(T)];
    }
}