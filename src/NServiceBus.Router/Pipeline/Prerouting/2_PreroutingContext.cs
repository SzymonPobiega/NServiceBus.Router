namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using System;

    /// <summary>
    /// Defines the context for the second part of the prerouting chain group -- the common prerouting chain.
    /// This chain is common to messages of all intents. It contains rules that detect
    /// messages of specific intents and fork to per-intent prerouting chains.
    /// </summary>
    public class PreroutingContext : BasePreroutingContext
    {
        internal bool Forwarded;
        internal bool Dropped;

        internal PreroutingContext(RawContext parent) : base(parent)
        {
            Body = parent.Body;
            Intent = GetMessageIntent(parent.Headers);
        }
        static MessageIntent? GetMessageIntent(IReadOnlyDictionary<string, string> headers)
        {
            if (headers.TryGetValue(NServiceBus.Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse<MessageIntent>(messageIntentString, true, out var messageIntent);
                return messageIntent;
            }
            return null;
        }

        /// <summary>
        /// Received message intent or null if message intent header was missing.
        /// </summary>
        public MessageIntent? Intent { get; }

        /// <summary>
        /// The body of the received message.
        /// </summary>
        public ReadOnlyMemory<byte> Body { get; set; }

        /// <summary>
        /// Mark this message as forwarded.
        /// </summary>
        /// <returns></returns>
        public void MarkForwarded()
        {
            Forwarded = true;
        }

        /// <summary>
        /// Marks this message as OK to be dropped if no chain terminator forwards it.
        /// </summary>
        public void DoNotRequireThisMessageToBeForwarded()
        {
            Dropped = true;
        }
    }
}