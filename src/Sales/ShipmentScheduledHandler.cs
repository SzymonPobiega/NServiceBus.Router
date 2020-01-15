using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class ShipmentScheduledHandler :
    IHandleMessages<ShipmentScheduled>
{
    public Task Handle(ShipmentScheduled message, IMessageHandlerContext context)
    {
        log.Info($"Order {message.OrderId} completed.");
       
        return context.Publish(new OrderCompleted
        {
            OrderId = message.OrderId
        });
    }

    static ILog log = LogManager.GetLogger<PlaceOrderHandler>();
}