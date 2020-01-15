using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class ScheduleShipmentHandler :
    IHandleMessages<ScheduleShipment>
{
    public Task Handle(ScheduleShipment message, IMessageHandlerContext context)
    {
        log.Info($"Scheduling shipment for order {message.OrderId}.");

        return context.Reply(new ScheduleShipmentResponse
        {
            OrderId = message.OrderId
        });
    }

    static ILog log = LogManager.GetLogger<ScheduleShipmentHandler>();
}