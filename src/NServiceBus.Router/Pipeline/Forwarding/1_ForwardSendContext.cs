namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class ForwardSendContext : BaseForwardRuleContext
    {

        public ForwardSendContext(string outgoingInterface, Route[] routes, SendPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
            MessageId = parentContext.MessageId;
        }
        public IReadOnlyCollection<Route> Routes { get; }
        public IReceivedMessageHeaders ReceivedHeaders { get; }
        public byte[] ReceivedBody { get; }
        public string MessageId { get; }
    }

    class ForwardSendTerminator : ChainTerminator<ForwardSendContext>
    {
        protected override Task Terminate(ForwardSendContext context)
        {
            return Task.CompletedTask;
        }
    }
}