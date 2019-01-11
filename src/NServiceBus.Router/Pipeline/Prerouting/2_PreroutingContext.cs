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
        internal PreroutingContext(RawContext parent) : base(parent)
        {
            Body = parent.Body;
            Intent = GetMesssageIntent(parent.Headers);
        }
        static MessageIntentEnum? GetMesssageIntent(IReadOnlyDictionary<string, string> headers)
        {
            var messageIntent = default(MessageIntentEnum);
            if (headers.TryGetValue(NServiceBus.Headers.MessageIntent, out var messageIntentString))
            {
                Enum.TryParse(messageIntentString, true, out messageIntent);
            }
            return messageIntent;
        }

        /// <summary>
        /// Received message intent or null if message intent header was missing.
        /// </summary>
        public MessageIntentEnum? Intent { get; }

        /// <summary>
        /// The body of the received message.
        /// </summary>
        public byte[] Body { get; set; }
    }
}