using NServiceBus.Router;

class OutboxDispatchContext : RuleContext
{
    public OutboxDispatchContext(RootContext parentContext, string @interface) : base(parentContext, @interface)
    {
    }
}