namespace NServiceBus.Router.Deduplication.Outbox
{
    class OutboxDispatchContext : RuleContext
    {
        public OutboxDispatchContext(RootContext parentContext, string @interface) : base(parentContext, @interface)
        {
        }
    }
}