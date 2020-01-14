using NServiceBus;

public class AuthorizeTransaction : ICommand
{
    public string OrderId { get; set; }
}