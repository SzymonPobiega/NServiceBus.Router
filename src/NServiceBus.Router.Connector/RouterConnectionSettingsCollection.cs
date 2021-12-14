using System.Collections.Generic;
using NServiceBus;

class RouterConnectionSettingsCollection
{
    Dictionary<string, RouterConnectionSettings> collection = new Dictionary<string, RouterConnectionSettings>();

    public IReadOnlyCollection<RouterConnectionSettings> Connections => collection.Values;

    public RouterConnectionSettings GetOrCreate(string routerAddress, bool enableAutoSubscribe, bool enableAutoPublish)
    {
        if (!collection.TryGetValue(routerAddress, out var value))
        {
            value = new RouterConnectionSettings(routerAddress, enableAutoSubscribe, enableAutoPublish);
            collection[routerAddress] = value;
        }

        return value;
    }
}
