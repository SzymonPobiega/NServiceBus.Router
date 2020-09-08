using System.Collections.Generic;
using NServiceBus;

class RouterConnectionSettingsCollection
{
    Dictionary<string, RouterConnectionSettings> collection = new Dictionary<string, RouterConnectionSettings>();

    public IReadOnlyCollection<RouterConnectionSettings> Connections => collection.Values;

    public RouterConnectionSettings GetOrCreate(string routerAddress)
    {
        if (!collection.TryGetValue(routerAddress, out var value))
        {
            value = new RouterConnectionSettings(routerAddress);
            collection[routerAddress] = value;
        }

        return value;
    }
}
