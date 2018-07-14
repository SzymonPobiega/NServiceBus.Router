namespace NServiceBus.Router
{
    public abstract class BaseForwardRuleContext : RuleContext
    {
        public string OutgoingInterface { get; }
        public string IncomingInterface { get; }

        protected BaseForwardRuleContext(string outgoingInterface, RuleContext parentContext) 
            : base(parentContext, outgoingInterface)
        {
            IncomingInterface = parentContext.Interface;
            OutgoingInterface = outgoingInterface;
        }
    }
}