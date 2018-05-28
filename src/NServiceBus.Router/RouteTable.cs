namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Logging;
    using Transport;

    /// <summary>
    /// Represents the route table. The routes are prioritized in the registration order i.e. routes registered earlier take precedence over route registered later.
    /// </summary>
    public class RouteTable
    {
        static ILog logger = LogManager.GetLogger<RouteTable>();
        string defaultGateway;
        string defaultGatwayIface;
        List<RouteTableEntry> entries = new List<RouteTableEntry>();

        /// <summary>
        /// Registers a default route.
        /// </summary>
        /// <param name="defaultGateway">Default gateway.</param>
        /// <param name="defaultGatwayIface">Default gateway's interface.</param>
        public void AddDefaultRoute(string defaultGateway, string defaultGatwayIface)
        {
            this.defaultGateway = defaultGateway ?? throw new ArgumentNullException(nameof(defaultGateway));
            this.defaultGatwayIface = defaultGatwayIface ?? throw new ArgumentNullException(nameof(defaultGatwayIface));
        }

        /// <summary>
        /// Adds a new route to the table.
        /// </summary>
        public void AddRoute(Func<string, Destination, bool> destinationFilter, string destinationFilterDescription, string gateway, string iface)
        {
            var entry = new RouteTableEntry(destinationFilter, destinationFilterDescription, gateway, iface);
            entries.Add(entry);

            logger.Debug($"Adding route {entry}.");
        }

        internal IEnumerable<string> GetOutgoingInterfaces(string incomingInterface, MessageContext context)
        {
            return GetDestinations(context).Select(d => GetOutgoingInterface(incomingInterface, d));
        }

        internal IEnumerable<Route> Route(string incomingInterface, MessageContext context)
        {
            return GetDestinations(context).Select(d =>
            {
                var nextHop = GetNextHop(incomingInterface, d);
                return new Route(d.Endpoint, nextHop);
            });
        }

        IEnumerable<Destination> GetDestinations(MessageContext context)
        {
            if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var destinationEndpoint))
            {
                throw new UnforwardableMessageException("No 'NServiceBus.Bridge.DestinationEndpoint' header.");
            }

            if (!context.Headers.TryGetValue("NServiceBus.Bridge.DestinationSites", out var sites))
            {
                var dest = new Destination(destinationEndpoint, null);
                yield return dest;
                yield break;
            }
            var siteArray = sites.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in siteArray)
            {
                var dest = new Destination(destinationEndpoint, s);
                yield return dest;
            }
        }

        internal string GetOutgoingInterface(string incomingInterface, Destination dest)
        {
            var matchingEntry = entries.FirstOrDefault(e => e.DestinationFilter(incomingInterface, dest));
            if (matchingEntry != null)
            {
                logger.Debug($"Using route {matchingEntry} to find outgoing interface for message to {dest} coming via {incomingInterface}: {matchingEntry.Iface}.");
            }
            else if (defaultGatwayIface != null)
            {
                logger.Debug($"Using default gateway interface for message to {dest} coming via {incomingInterface}: {defaultGatwayIface}.");
            }
            var outgoingInterface = matchingEntry?.Iface ?? defaultGatwayIface;
            return outgoingInterface ?? throw new UnforwardableMessageException($"No route for destination {dest}");
        }

        internal string GetNextHop(string incomingInterface, Destination dest)
        {
            var matchingEntry = entries.FirstOrDefault(e => e.DestinationFilter(incomingInterface, dest));
            if (matchingEntry != null)
            {
                logger.Debug($"Using route {matchingEntry} to find next hop for message to {dest} coming via {incomingInterface}: {matchingEntry.Gateway}.");
            }
            else if (defaultGateway != null)
            {
                logger.Debug($"Using default gateway for message to {dest} coming via {incomingInterface}: {defaultGateway}.");
            }
            else
            {
                logger.Debug($"Sending message to {dest} directly.");
            }
            var gateway = matchingEntry?.Gateway ?? defaultGateway;
            return gateway;
        }
    }
}