using NServiceBus;

public class ScheduleShipmentResponse : IMessage
{
    public string OrderId { get; set; }
}