namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Features;
    using Routing;

    class RoutingMonitor : FeatureStartupTask
    {
        UnicastRoutingTable routingTable;
        CriticalError criticalError;
        Task monitorTask;
        Dictionary<Type, RouteState> routeState;
        CancellationTokenSource stopTokenSource;

        public RoutingMonitor(UnicastRoutingTable routingTable, Dictionary<Type, string> endpointMap, UnicastRoute routeToRouter, CriticalError criticalError)
        {
            this.routingTable = routingTable;
            this.criticalError = criticalError;
            routeState = endpointMap.ToDictionary(kvp => kvp.Key, kvp => new RouteState(routeToRouter, kvp.Key, kvp.Value));
        }

        protected override Task OnStart(IMessageSession session)
        {
            stopTokenSource = new CancellationTokenSource();
            monitorTask = Task.Run(async () =>
            {
                while (!stopTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        var routesChanged = routeState.Values.Count(x => x.Tick(DateTime.UtcNow));
                        if (routesChanged > 0)
                        {
                            UpdateRoutes();
                        }
                    }
                    catch (Exception e)
                    {
                        criticalError.Raise("Unexpected error while updating routing table", e);
                    }

                    await Task.Delay(1000).ConfigureAwait(false);
                }
            });
            return Task.CompletedTask;
        }

        void UpdateRoutes()
        {
            var entries = routeState.Values.Select(r => r.CreateEntry()).ToList();
            routingTable.AddOrReplaceRoutes("NServiceBus.Router.Migrator", entries);
        }

        protected override async Task OnStop(IMessageSession session)
        {
            stopTokenSource.Cancel();
            await monitorTask.ConfigureAwait(false);
            stopTokenSource.Dispose();
        }

        public bool Fallback(Type messageMessageType)
        {
            if (routeState.TryGetValue(messageMessageType, out var state))
            {
                state.Fallback();
                UpdateRoutes();
                return true;
            }

            return false;
        }

        public void ContinueMonitoring(Type messageMessageType)
        {
            //Instructs monitor to continue monitoring the given type if it has not been known to be on the new transport
            if (routeState.TryGetValue(messageMessageType, out var state))
            {
                state.ContinueMonitoring(DateTime.UtcNow);
            }
        }
    }
}