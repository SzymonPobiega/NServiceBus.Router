using System;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

class RoutingHeadersBehavior : Behavior<IOutgoingSendContext>
{
    readonly CompiledRouterConnectionSettings compiledSettings;

    public RoutingHeadersBehavior(CompiledRouterConnectionSettings compiledSettings)
    {
        this.compiledSettings = compiledSettings;
    }

    public override Task Invoke(IOutgoingSendContext context, Func<Task> next)
    {
        if (compiledSettings.TryGetDestination(context.Message.MessageType, out var ultimateDestination))
        {
            context.Headers["NServiceBus.Bridge.DestinationEndpoint"] = ultimateDestination.Endpoint;
        }
        return next();
    }
}