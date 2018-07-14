using System;
using System.Collections.Generic;
using NServiceBus.Router;

class FindDestinationsByHeadersRule
{
    protected static IEnumerable<Destination> Find(IReadOnlyDictionary<string, string> headers)
    {
        headers.TryGetValue("NServiceBus.Bridge.DestinationEndpoint", out var destinationEndpoint);
        if (!headers.TryGetValue("NServiceBus.Bridge.DestinationSites", out var sites))
        {
            if (destinationEndpoint != null)
            {
                yield return new Destination(destinationEndpoint, null);
            }
        }
        else
        {
            var siteArray = sites.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in siteArray)
            {
                var dest = new Destination(destinationEndpoint, s);
                yield return dest;
            }
        }
        
    }
}
