namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Threading.Tasks;

    class ShadowSendDestinationRule : IRule<SendPreroutingContext, SendPreroutingContext>
    {
        string fixedDestination;

        public ShadowSendDestinationRule(string fixedDestination)
        {
            this.fixedDestination = fixedDestination;
        }

        public Task Invoke(SendPreroutingContext context, Func<SendPreroutingContext, Task> next)
        {
            context.Destinations.Add(new Destination(fixedDestination, null));
            return next(context);
        }
    }
}