class DestinationInfo
{
    public DestinationInfo(string endpoint, string router)
    {
        Endpoint = endpoint;
        Router = router;
    }

    public string Endpoint { get; }
    public string Router { get; }
}