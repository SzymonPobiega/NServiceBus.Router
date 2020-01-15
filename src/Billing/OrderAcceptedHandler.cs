using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class OrderAcceptedHandler :
    IHandleMessages<OrderAccepted>
{
    public Task Handle(OrderAccepted message, IMessageHandlerContext context)
    {
        log.Info($"Attempting to authorize the payment transaction for order {message.OrderId}.");

        return context.Send(new AuthorizeTransaction
        {
            OrderId = message.OrderId
        });
    }

    static ILog log = LogManager.GetLogger<OrderAcceptedHandler>();
}