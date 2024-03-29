using System;
using System.Collections.Generic;

namespace NServiceBus
{
    /// <summary>
    /// Configures the connection to the router.
    /// </summary>
    public class RouterConnectionSettings
    {
        internal RouterConnectionSettings(string routerAddress, bool enableAutoSubscribe, bool enableAutoPublish)
        {
            RouterAddress = routerAddress;
            this.EnableAutoSubscribe = enableAutoSubscribe;
            this.EnableAutoPublish = enableAutoPublish;
        }

        /// <summary>
        /// Delegates routing of a given message type to the router.
        /// </summary>
        /// <param name="messageType">Message type.</param>
        public void DelegateRouting(Type messageType)
        {
            SendRouteTable[messageType] = null;
        }

        /// <summary>
        /// Instructs the endpoint to route messages of this type to a designated endpoint via the router.
        /// </summary>
        /// <param name="messageType">Message type.</param>
        /// <param name="endpointName">Name of the destination endpoint.</param>
        public void RouteToEndpoint(Type messageType, string endpointName)
        {
            SendRouteTable[messageType] = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
        }

        /// <summary>
        /// Registers a designated endpoint as a publisher of the events of this type. The endpoint will be used as a destination of subscribe messages. The subscribe
        /// message will be sent via the router.
        /// </summary>
        /// <param name="eventType">Type of the event.</param>
        /// <param name="publisherEndpointName">Name of the publishing endpoint.</param>
        public void RegisterPublisher(Type eventType, string publisherEndpointName)
        {
            PublisherTable[eventType] = publisherEndpointName;
        }

        internal string RouterAddress;
        internal bool EnableAutoSubscribe;
        internal bool EnableAutoPublish;
        internal Dictionary<Type, string> SendRouteTable = new Dictionary<Type, string>();
        internal Dictionary<Type, string> PublisherTable = new Dictionary<Type, string>();
    }
}