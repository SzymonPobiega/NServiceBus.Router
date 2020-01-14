using NServiceBus;

public class AuthorizeTransactionResponse : ICommand
{
    public string OrderId { get; set; }
}