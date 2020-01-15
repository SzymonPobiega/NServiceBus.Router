namespace NServiceBus.Router.Migrator
{
    using System.Threading.Tasks;
    using Routing;
    using Transport;


    class ForwardPublishByDestinationAddressRule : ChainTerminator<ForwardPublishContext>
    {
        protected override async Task<bool> Terminate(ForwardPublishContext context)
        {
            if (context.ReceivedHeaders.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var destinationEndpoint))
            {
                context.ForwardedHeaders["NServiceBus.Router.Migrator.Forwarded"] = "true";

                var op = new TransportOperation(new OutgoingMessage(context.MessageId, context.ForwardedHeaders, context.ReceivedBody),
                    new UnicastAddressTag(destinationEndpoint));

                var postroutingContext = new PostroutingContext(null, op, context);
                var chain = context.Chains.Get<PostroutingContext>();

                await chain.Invoke(postroutingContext).ConfigureAwait(false);

                return true;
            }

            return false;
        }
    }
}
