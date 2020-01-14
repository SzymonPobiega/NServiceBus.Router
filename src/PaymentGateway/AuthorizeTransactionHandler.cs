using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;

class AuthorizeTransactionHandler :
    IHandleMessages<AuthorizeTransaction>
{
    public Task Handle(AuthorizeTransaction message, IMessageHandlerContext context)
    {
        log.Info($"Authorizing payment transaction for order {message.OrderId}.");

        return context.Reply(new AuthorizeTransactionResponse
        {
            OrderId = message.OrderId
        });
    }

    static ILog log = LogManager.GetLogger<AuthorizeTransactionHandler>();
}