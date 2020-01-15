using NServiceBus;

public class OrderCompleted : IEvent
{
    public string OrderId { get; set; }
}