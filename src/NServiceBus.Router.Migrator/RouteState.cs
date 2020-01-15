namespace NServiceBus.Router.Migrator
{
    using System;
    using Routing;

    class RouteState
    {
        RouteTableEntry routeToRouter;
        RouteTableEntry directRoute;
        bool useDirectRoute;
        bool monitorDirectRoute;
        DateTime? timeToCheck;

        public RouteState(UnicastRoute routeToRouter, Type messageType, string endpointName)
        {
            this.routeToRouter = new RouteTableEntry(messageType, routeToRouter);
            directRoute = new RouteTableEntry(messageType, UnicastRoute.CreateFromEndpointName(endpointName));
            useDirectRoute = true;
        }

        public void Fallback()
        {
            useDirectRoute = false;
            monitorDirectRoute = false;
            timeToCheck = null;
        }

        public void ContinueMonitoring(DateTime now)
        {
            useDirectRoute = false;
            monitorDirectRoute = true;
            timeToCheck = now.AddSeconds(60);
        }

        public bool Tick(DateTime now)
        {
            if (monitorDirectRoute && timeToCheck < now)
            {
                monitorDirectRoute = false;
                useDirectRoute = true;
                timeToCheck = null;
                return true;
            }
            return false;
        }

        public RouteTableEntry CreateEntry()
        {
            return useDirectRoute ? directRoute : routeToRouter;
        }
    }
}