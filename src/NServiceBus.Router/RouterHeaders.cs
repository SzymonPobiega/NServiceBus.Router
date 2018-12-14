class RouterHeaders
{
    public const string ReplyToRouter = "NServiceBus.Router.ReplyTo";
    public const string Plug = "NServiceBus.Router.Plug";
    public const string SequenceKey = "NServiceBus.Router.SequenceKey";
    public const string SequenceNumber = "NServiceBus.Router.SequenceNumber";
    public const string Initialize = "NServiceBus.Router.Initialize";
    public const string Advance = "NServiceBus.Router.Advance";

    public const string InitializeHeadLo = "NServiceBus.Router.Initialize.Head.Lo";
    public const string InitializeHeadHi = "NServiceBus.Router.Initialize.Head.Hi";
    public const string InitializeTailLo = "NServiceBus.Router.Initialize.Tail.Lo";
    public const string InitializeTailHi = "NServiceBus.Router.Initialize.Tail.Hi";
    public const string AdvanceHeadLo = "NServiceBus.Router.Advance.Head.Lo";
    public const string AdvanceHeadHi = "NServiceBus.Router.Advance.Head.Hi";
    public const string AdvanceEpoch = "NServiceBus.Router.Advance.Epoch";
}