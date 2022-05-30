using System;
using System.Collections.Generic;

class CompiledRouterConnectionSettings
{
    Dictionary<Type, DestinationInfo> eventRouting = new Dictionary<Type, DestinationInfo>();
    Dictionary<Type, DestinationInfo> commandRouting = new Dictionary<Type, DestinationInfo>();
    List<string> autoSubscribeRouters = new List<string>();
    List<string> autoPublishRouters = new List<string>();
    string defaultRouter;

    public CompiledRouterConnectionSettings(RouterConnectionSettingsCollection collection)
    {
        foreach (var router in collection.Connections)
        {
            if (defaultRouter == null)
            {
                defaultRouter = router.RouterAddress;
            }

            if (router.EnableAutoSubscribe)
            {
                autoSubscribeRouters.Add(router.RouterAddress);
            }

            if (router.EnableAutoPublish)
            {
                autoPublishRouters.Add(router.RouterAddress);
            }

            foreach (var publisherEntry in router.PublisherTable)
            {
                if (eventRouting.TryGetValue(publisherEntry.Key, out var publisherInfo))
                {
                    throw new Exception($"Event {publisherEntry.Key} is already associated with endpoint {publisherInfo.Router} via router {publisherInfo.Router}.");
                }
                eventRouting[publisherEntry.Key] = new DestinationInfo(publisherEntry.Value, router.RouterAddress);
            }

            foreach (var receiverEntry in router.SendRouteTable)
            {
                if (commandRouting.TryGetValue(receiverEntry.Key, out var receiverInfo))
                {
                    throw new Exception($"Message {receiverEntry.Key} is already associated with endpoint {receiverInfo.Router} via router {receiverInfo.Router}.");
                }
                commandRouting[receiverEntry.Key] = new DestinationInfo(receiverEntry.Value, router.RouterAddress);
            }
        }
    }

    public IEnumerable<string> AutoSubscribeRouters => autoSubscribeRouters;
    public IEnumerable<string> AutoPublishRouters => autoPublishRouters;

    public bool TryGetPublisher(Type eventType, out DestinationInfo destinationInfo)
    {
        return eventRouting.TryGetValue(eventType, out destinationInfo);
    }

    public bool TryGetDestination(Type eventType, out DestinationInfo destinationInfo)
    {
        return commandRouting.TryGetValue(eventType, out destinationInfo);
    }
}

