using System;

class Route
{
    public Route(string destination, string gateway)
    {
        if (destination == null && gateway == null)
        {
            throw new ArgumentException("Either destination or gateway has to be specified.");
        }
        Destination = destination;
        Gateway = gateway;
    }

    public string Gateway { get; }
    public string Destination { get; }
}
