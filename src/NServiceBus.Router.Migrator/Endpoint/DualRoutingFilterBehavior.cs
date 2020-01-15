namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Pipeline;

    class DualRoutingFilterBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        string endpointName;

        public DualRoutingFilterBehavior(string endpointName)
        {
            this.endpointName = endpointName;
        }

        public override Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            //Do not ignore messages that were delivered via message-driven mechanism
            if (!context.MessageHeaders.TryGetValue("NServiceBus.Router.Migrator.Ignore", out var ignores)
                || context.MessageHeaders.ContainsKey("NServiceBus.Router.Migrator.Forwarded"))
            {
                return next();
            }

            var ignoreAddresses = ignores.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (ignoreAddresses.Contains(endpointName))
            {
                //This messages has also been sent via message-driven pub sub so we can ignore this one
                return Task.CompletedTask;
            }

            return next();
        }
    }
}