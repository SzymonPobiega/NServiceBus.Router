namespace NServiceBus.Router.Deduplication
{
    enum WatermarkCheckViolationResult
    {
        Duplicate,
        Retry
    }
}