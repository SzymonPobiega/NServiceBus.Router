using NServiceBus;

public class PaymentCompleted : IEvent
{
    public string OrderId { get; set; }
}