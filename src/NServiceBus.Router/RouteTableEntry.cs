using System;
using NServiceBus.Router;

class RouteTableEntry
{
    public RouteTableEntry(Func<string, Destination, bool> destinationFilter, string destinationFilterDescription, string gateway, string iface)
    {
        DestinationFilter = destinationFilter;
        Gateway = gateway;
        Iface = iface;
        DestinationFilterDescription = destinationFilterDescription;
    }

    public string DestinationFilterDescription { get; }

    public Func<string, Destination, bool> DestinationFilter { get; }
    public string Gateway { get; }
    public string Iface { get; }

    public override string ToString()
    {
        var gatewayPart = Gateway != null
            ? $":{Gateway}"
            : "";
        return $"{DestinationFilterDescription}:{Iface}{gatewayPart}";
    }
}
