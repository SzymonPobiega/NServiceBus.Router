using System.Collections;
using System.Collections.Generic;
using NServiceBus.Router;

class ReceivedMessageHeaders : IReceivedMessageHeaders
{
    Dictionary<string, string> storage;

    public ReceivedMessageHeaders(Dictionary<string, string> receivedHeaders)
    {
        storage = receivedHeaders;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return storage.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)storage).GetEnumerator();
    }

    public Dictionary<string, string> Copy()
    {
        return new Dictionary<string, string>(storage);
    }

    public int Count => storage.Count;

    public bool ContainsKey(string key)
    {
        return storage.ContainsKey(key);
    }

    public bool TryGetValue(string key, out string value)
    {
        return storage.TryGetValue(key, out value);
    }

    public string this[string key] => storage[key];

    public IEnumerable<string> Keys => storage.Keys;

    public IEnumerable<string> Values => storage.Values;
}