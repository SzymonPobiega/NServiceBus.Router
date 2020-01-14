using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class OrderCompletedHandler :
    IHandleMessages<OrderCompleted>
{
    public Task Handle(OrderCompleted message, IMessageHandlerContext context)
    {
        log.Info($"Order {message.OrderId} completed.");
        return Task.CompletedTask;
    }

    static ILog log = LogManager.GetLogger<OrderCompletedHandler>();
}
