using System.Collections.Generic;

namespace NServiceBus.Router
{
    using System;
    using System.Linq;
    using Transport;

    /// <summary>
    /// Determines the next hop(s) when routing between bridges.
    /// </summary>
    /// <param name="context">The message being forwarded.</param>
    /// <param name="messageType">Message type.</param>
    /// <returns>Name of next hop endpoint or null if could not determine.</returns>
    public delegate string[] DetermineNextHop(MessageContext context, string messageType);

    /// <summary>
    /// Configures the connection to the bridge.
    /// </summary>
    public class InterBridgeRoutingSettings
    {
        internal InterBridgeRoutingSettings()
        {
            routingCallbacks.Add((context, type) => RoutingTable.TryGetValue(type, out var sendDestination) ? sendDestination : null);
        }

        /// <summary>
        /// Instructs the endpoint to route messages of this type to a designated endpoint on the other side of the bridge.
        /// </summary>
        /// <param name="messageType">Message type.</param>
        /// <param name="nextHop">Name of the next hop.</param>
        public void ForwardTo(string messageType, string nextHop)
        {
            ValidateIfAlreadyConfigured(messageType, nextHop);
            RoutingTable[messageType] = new[]{nextHop};
        }

        
        /// <summary>
        /// Registers a designated endpoint as a publisher of the events of this type. The endpoint will be used as a destination of subscribe messages.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <param name="nextHop">Name of the next hop.</param>
        public void RegisterPublisher(string eventType, string nextHop)
        {
            ValidateIfAlreadyConfigured(eventType, nextHop);
            RoutingTable[eventType] = new [] {nextHop};
        }

        void ValidateIfAlreadyConfigured(string messageType, string nextHop)
        {
            if (RoutingTable.ContainsKey(messageType))
            {
                throw new Exception($"Cannot configure routing of message {messageType} to {nextHop}. This message routing destination is configured to {RoutingTable[messageType].First()}.");
            }
        }

        /// <summary>
        /// Registers a new callback for determining the next hop when routing between bridges.
        /// </summary>
        public void RegisterRoutingCallback(DetermineNextHop routingCallback)
        {
            if (routingCallback == null)
            {
                throw new ArgumentNullException(nameof(routingCallback));
            }
            routingCallbacks.Insert(0, routingCallback);
        }

        internal bool TryGetDestination(MessageContext context, string messageType, out string[] nextHops)
        {
            nextHops = routingCallbacks.Select(x => x(context, messageType)).FirstOrDefault(d => d != null);
            return nextHops != null;
        }

        List<DetermineNextHop> routingCallbacks = new List<DetermineNextHop>();
        Dictionary<string, string[]> RoutingTable = new Dictionary<string, string[]>();
    }
}