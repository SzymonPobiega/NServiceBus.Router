using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

namespace NServiceBus.Router.Migrator
{
    class MigratorRouterDestinationBehavior : Behavior<IOutgoingSendContext>
    {
        Dictionary<Type, string> routeTable;

        public MigratorRouterDestinationBehavior(Dictionary<Type, string> routeTable)
        {
            this.routeTable = routeTable;
        }

        public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
        {
            if (routeTable.TryGetValue(context.Message.MessageType, out var ultimateDestination) && ultimateDestination != null)
            {
                context.Headers["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination;
            }
            return next();
        }
    }
}
