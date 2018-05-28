using System;

class Route
{
    public Route(string destination, string gateway)
    {
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));
        Gateway = gateway;
    }

    public string Gateway { get; }
    public string Destination { get; }
}
