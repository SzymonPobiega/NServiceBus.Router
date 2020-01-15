namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Threading.Tasks;

    class ForwardSendRule : IRule<ForwardSendContext, ForwardSendContext>
    {
        public Task Invoke(ForwardSendContext context, Func<ForwardSendContext, Task> next)
        {
            context.ForwardedHeaders.Remove("NServiceBus.Bridge.DestinationEndpoint");
            return next(context);
        }
    }
}