using System;
using System.Threading.Tasks;
using NServiceBus.Router;

class Chain<T> : IChain<T>
    where T : IRuleContext
{
    Func<T, Task> invokeDelegate;

    public Chain(Func<T, Task> invokeDelegate)
    {
        this.invokeDelegate = invokeDelegate;
    }

    public Task Invoke(T context)
    {
        return invokeDelegate(context);
    }
}