namespace NServiceBus.Router
{
    /// <summary>
    /// Convenient routes
    /// </summary>
    public static class RouteTableExtensions
    {
        /// <summary>
        /// Adds a generic rule to forward all traffic from <paramref name="incomingInterface"/> to <paramref name="outgoingInterface"/> optionally providing the gateway.
        /// </summary>
        public static void AddForwardRoute(this RouteTable routeTable, string incomingInterface, string outgoingInterface, string gateway = null)
        {
            routeTable.AddRoute((i, d) => i == incomingInterface,$"Interface = {incomingInterface}", gateway, outgoingInterface);
        }
    }
}