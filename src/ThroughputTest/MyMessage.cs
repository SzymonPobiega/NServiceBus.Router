using NServiceBus;

class MyMessage : IMessage
{
    public byte[] Data { get; set; }
}