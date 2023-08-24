﻿namespace NServiceBus.Router
{
    using System;
    using Transport;

    /// <summary>
    /// Defines the context for the first part of the prerouting chain group -- the raw chain.
    /// </summary>
    public class RawContext : BasePreroutingContext
    {
        internal RawContext(MessageContext messageContext, string incomingInterface, RootContext root) 
            : base(root, incomingInterface, new ReceivedMessageHeaders(messageContext.Headers), messageContext.NativeMessageId)
        {
            Set(messageContext.TransportTransaction);

            Body = messageContext.Body;
        }

        /// <summary>
        /// The body of the received message.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; }
    }
}