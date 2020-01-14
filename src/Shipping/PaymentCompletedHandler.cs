using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class PaymentCompletedHandler :
    IHandleMessages<PaymentCompleted>
{
    public Task Handle(PaymentCompleted message, IMessageHandlerContext context)
    {
        log.Info($"Attempting to schedule shipment for order {message.OrderId}.");

        return context.Send(new ScheduleShipment
        {
            OrderId = message.OrderId
        });
    }

    static ILog log = LogManager.GetLogger<PaymentCompletedHandler>();
}