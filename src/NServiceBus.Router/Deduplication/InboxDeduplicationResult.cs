namespace NServiceBus.Router.Deduplication
{
    enum InboxDeduplicationResult
    {
        OK,
        Duplicate,
        WatermarkViolation,
    }
}