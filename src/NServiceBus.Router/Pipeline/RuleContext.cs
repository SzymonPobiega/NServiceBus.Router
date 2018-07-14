namespace NServiceBus.Router
{
    using Extensibility;

    public abstract class RuleContext : ContextBag, IRuleContext
    {
        protected RuleContext(RuleContext parentContext, string @interface = null) 
            : base(parentContext?.Extensions)
        {
            Interface = @interface ?? parentContext.Interface;
        }

        internal RuleContext(RootContext parentContext, string @interface)
            : base(parentContext?.Extensions)
        {
            Interface = @interface;
        }

        public string Interface { get; }

        public IChains Chains => Extensions.Get<IInterfaceChains>().GetChainsFor(Interface);

        public ContextBag Extensions => this;
    }
}