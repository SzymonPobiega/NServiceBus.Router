using NServiceBus;

public class ShipmentScheduled : IEvent
{
    public string OrderId { get; set; }
}