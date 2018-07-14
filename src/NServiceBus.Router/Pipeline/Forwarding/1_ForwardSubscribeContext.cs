namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ForwardSubscribeContext : BaseForwardRuleContext
    {
        public IReadOnlyCollection<Route> Routes { get; }
        public string MessageType { get; }

        public ForwardSubscribeContext(string outgoingInterface, Route[] routes, SubscribePreroutingContext parentContext) 
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            MessageType = parentContext.MessageType;
        }
    }

    class ForwardSubscribeTerminator : ChainTerminator<ForwardSubscribeContext>
    {
        protected override Task Terminate(ForwardSubscribeContext context)
        {
            return Task.CompletedTask;
        }
    }
}