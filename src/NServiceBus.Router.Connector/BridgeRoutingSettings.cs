using System;
using System.Collections.Generic;

namespace NServiceBus
{
    /// <summary>
    /// Configures the connection to the bridge.
    /// </summary>
    public class BridgeRoutingSettings
    {
        internal BridgeRoutingSettings(string bridgeAddress)
        {
            BridgeAddress = bridgeAddress;
        }

        /// <summary>
        /// Instructs the endpoint to route messages of this type to a designated endpoint on the other side of the bridge.
        /// </summary>
        /// <param name="messageType">Message type.</param>
        /// <param name="endpointName">Name of the destination endpoint.</param>
        public void RouteToEndpoint(Type messageType, string endpointName)
        {
            SendRouteTable[messageType] = endpointName;
        }

        /// <summary>
        /// Registers a designated endpoint as a publisher of the events of this type. The endpoint will be used as a destination of subscribe messages.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <param name="publisherEndpointName">Name of the publishing endpoint.</param>
        public void RegisterPublisher(Type eventType, string publisherEndpointName)
        {
            PublisherTable[eventType] = publisherEndpointName;
        }

        /// <summary>
        /// Associates the endpoint with the name of the port when connecting to a switch.
        /// </summary>
        /// <param name="endpointName">Name of the endpoint.</param>
        /// <param name="port">Name of the switch port that the endpoint is connected to.</param>
        public void SetPort(string endpointName, string port)
        {
            if (string.IsNullOrEmpty(endpointName))
            {
                throw new ArgumentException("Endpoint name cannot be an empty.", nameof(endpointName));
            }

            if (string.IsNullOrEmpty(port))
            {
                throw new ArgumentException("Port name cannot be an empty.", nameof(port));
            }
        }

        internal string BridgeAddress;
        internal Dictionary<Type, string> SendRouteTable = new Dictionary<Type, string>();
        internal Dictionary<Type, string> PublisherTable = new Dictionary<Type, string>();
    }
}