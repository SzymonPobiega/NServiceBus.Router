using System;
using System.Collections.Generic;

namespace NServiceBus
{
    /// <summary>
    /// Configures the connection to the router.
    /// </summary>
    public class RouterConnectionSettings
    {
        internal RouterConnectionSettings(string routerAddress)
        {
            RouterAddress = routerAddress;
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

        /// <summary>
        /// Associate selected destination sites with this router connection
        /// </summary>
        /// <param name="sites"></param>
        public void AssociateSites(params string[] sites)
        {
            foreach (var site in sites)
            {
                AssociatedSites.Add(site);
            }
        }

        internal string RouterAddress;
        internal Dictionary<Type, string> SendRouteTable = new Dictionary<Type, string>();
        internal Dictionary<Type, string> PublisherTable = new Dictionary<Type, string>();
        internal HashSet<string> AssociatedSites = new HashSet<string>();
    }
}