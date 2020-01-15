using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class ScheduleShipmentResponseHandler :
    IHandleMessages<ScheduleShipmentResponse>
{
    public Task Handle(ScheduleShipmentResponse message, IMessageHandlerContext context)
    {
        log.Info($"Shipment for order {message.OrderId} scheduled.");

        return context.Publish(new ShipmentScheduled
        {
            OrderId = message.OrderId
        });
    }

    static ILog log = LogManager.GetLogger<PaymentCompletedHandler>();
}