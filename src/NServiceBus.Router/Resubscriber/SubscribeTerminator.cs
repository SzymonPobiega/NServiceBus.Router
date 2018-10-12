using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Raw;

namespace NServiceBus.Router.Resubscriber
{
    class SubscribeTerminator : ChainTerminator<SubscribeContext>
    {
        IRawEndpoint endpoint;
        RuntimeTypeGenerator typeGenerator;
        ILog logger = LogManager.GetLogger<SubscribeTerminator>();

        public SubscribeTerminator(IRawEndpoint endpoint, RuntimeTypeGenerator typeGenerator)
        {
            this.endpoint = endpoint;
            this.typeGenerator = typeGenerator;
        }

        protected override Task Terminate(SubscribeContext context)
        {
            logger.Debug($"Resubscribing to {context.MessageType}.");
            var type = typeGenerator.GetType(context.MessageType);
            return endpoint.SubscriptionManager.Subscribe(type, context);
        }
    }
}
