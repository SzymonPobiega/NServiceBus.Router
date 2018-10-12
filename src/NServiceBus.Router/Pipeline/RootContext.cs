using NServiceBus.Extensibility;
using NServiceBus.Router; 

class RootContext : ContextBag, IRuleContext
{
    public RootContext(IInterfaceChains interfaces)
    {
        Set(interfaces);
    }

    public ContextBag Extensions => this;
}
