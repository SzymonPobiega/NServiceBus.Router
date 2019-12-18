namespace NServiceBus.Router.Migrator
{
    using System.Threading.Tasks;
    using Routing;
    using Transport;

    class ShadowForwardPublishRule : ChainTerminator<ForwardPublishContext>
    {
        string fixedDestination;

        public ShadowForwardPublishRule(string fixedDestination)
        {
            this.fixedDestination = fixedDestination;
        }

        protected override async Task<bool> Terminate(ForwardPublishContext context)
        {
            var op = new TransportOperation(new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody),
                new UnicastAddressTag(fixedDestination));

            var postroutingContext = new PostroutingContext(null, op, context);
            var chain = context.Chains.Get<PostroutingContext>();

            await chain.Invoke(postroutingContext).ConfigureAwait(false);

            return true;
        }
    }
}