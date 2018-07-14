namespace NServiceBus.Router
{
    using System.Threading.Tasks;

    public class ForwardPublishContext : BaseForwardRuleContext
    {
        public ForwardPublishContext(string outgoingInterface, PublishPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
            MessageId = parentContext.MessageId;
            Types = parentContext.Types;
        }

        public string[] Types { get; }
        public IReceivedMessageHeaders ReceivedHeaders { get; }
        public byte[] ReceivedBody { get; }
        public string MessageId { get; }
    }

    class ForwardPublishTerminator : ChainTerminator<ForwardPublishContext>
    {
        protected override Task Terminate(ForwardPublishContext context)
        {
            return Task.CompletedTask;
        }
    }
}