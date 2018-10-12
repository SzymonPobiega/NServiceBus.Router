namespace NServiceBus.Router.Deduplication
{
    class OutboxDispatchContext : RuleContext
    {
        public OutboxDispatchContext(RootContext parentContext, string @interface) : base(parentContext, @interface)
        {
        }
    }
}