namespace NServiceBus.Router
{
    using System.Threading.Tasks;

    public class ForwardReplyContext : BaseForwardRuleContext
    {
        public ForwardReplyContext(string outgoingInterface, ReplyPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
            MessageId = parentContext.MessageId;
        }

        public IReceivedMessageHeaders ReceivedHeaders { get; }
        public byte[] ReceivedBody { get; }
        public string MessageId { get; }
    }

    class ForwardReplyTerminator : ChainTerminator<ForwardReplyContext>
    {
        protected override Task Terminate(ForwardReplyContext context)
        {
            return Task.CompletedTask;
        }
    }
}