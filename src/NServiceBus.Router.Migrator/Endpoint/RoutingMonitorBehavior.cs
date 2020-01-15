namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class RoutingMonitorBehavior : Behavior<IOutgoingSendContext>
    {
        RoutingMonitor routingMonitor;

        public RoutingMonitorBehavior(RoutingMonitor routingMonitor)
        {
            this.routingMonitor = routingMonitor;
        }

        public override async Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            var retry = false;
            try
            {
                await next().ConfigureAwait(false);
            }
            catch (Exception)
            {
                retry = routingMonitor.Fallback(context.Message.MessageType);
                if (retry)
                {
                    await next().ConfigureAwait(false);
                }
            }
            finally
            {
                if (retry)
                {
                    routingMonitor.ContinueMonitoring(context.Message.MessageType);
                }
            }
        }
    }
}