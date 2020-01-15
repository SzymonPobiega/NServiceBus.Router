using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class AuthorizeTransactionResponseHandler :
    IHandleMessages<AuthorizeTransactionResponse>
{
    public Task Handle(AuthorizeTransactionResponse message, IMessageHandlerContext context)
    {
        log.Info($"Payment transaction for order {message.OrderId} authorized.");

        return context.Publish(new PaymentCompleted
        {
            OrderId = message.OrderId
        });
    }

    static ILog log = LogManager.GetLogger<OrderAcceptedHandler>();
}