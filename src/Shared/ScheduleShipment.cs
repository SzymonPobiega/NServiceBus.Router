using NServiceBus;

public class ScheduleShipment : ICommand
{
    public string OrderId { get; set; }
}