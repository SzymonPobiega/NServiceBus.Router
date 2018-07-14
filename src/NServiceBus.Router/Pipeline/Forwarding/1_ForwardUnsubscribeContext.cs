namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ForwardUnsubscribeContext : BaseForwardRuleContext
    {
        public IReadOnlyCollection<Route> Routes { get; }
        public string MessageType { get; }

        public ForwardUnsubscribeContext(string outgoingInterface, Route[] routes, UnsubscribePreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            MessageType = parentContext.MessageType;
        }
    }

    class ForwardUnsubscribeTerminator : ChainTerminator<ForwardUnsubscribeContext>
    {
        protected override Task Terminate(ForwardUnsubscribeContext context)
        {
            return Task.CompletedTask;
        }
    }
}