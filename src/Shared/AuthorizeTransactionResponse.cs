using NServiceBus;

public class AuthorizeTransactionResponse : IMessage
{
    public string OrderId { get; set; }
}