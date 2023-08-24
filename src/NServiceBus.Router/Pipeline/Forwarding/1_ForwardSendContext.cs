namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines the context for the forward send chain.
    /// </summary>
    public class ForwardSendContext : BaseForwardRuleContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        public ForwardSendContext(string outgoingInterface, Route[] routes, SendPreroutingContext parentContext)
            : base(outgoingInterface, parentContext)
        {
            Routes = routes;
            ReceivedHeaders = parentContext.Headers;
            ReceivedBody = parentContext.Body;
        }

        /// <summary>
        /// The routes calculated for the message.
        /// </summary>
        public IReadOnlyCollection<Route> Routes { get; }

        /// <summary>
        /// The headers associated with the received message.
        /// </summary>
        public IReceivedMessageHeaders ReceivedHeaders { get; }

        /// <summary>
        /// The headers associated with the received message.
        /// </summary>
        public ReadOnlyMemory<byte> ReceivedBody { get; }
    }
}