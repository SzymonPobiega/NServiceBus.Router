namespace NServiceBus.Router.Deduplication.Outbox
{
    enum HoleType
    {
        MissingRow,
        UndispatchedRow
    }
}