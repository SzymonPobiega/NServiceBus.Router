using NServiceBus.Extensibility;

namespace NServiceBus.Router.Resubscriber
{
    class SubscribeContext : ContextBag, IRuleContext
    {
        public SubscribeContext(string messageType)
        {
            MessageType = messageType;
        }

        public string MessageType { get; }
        public ContextBag Extensions => this;
    }
}