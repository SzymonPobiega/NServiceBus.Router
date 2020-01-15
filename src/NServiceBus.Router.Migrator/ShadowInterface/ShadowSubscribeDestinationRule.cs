namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Threading.Tasks;

    class ShadowSubscribeDestinationRule : IRule<SubscribePreroutingContext, SubscribePreroutingContext>
    {
        string fixedDestination;

        public ShadowSubscribeDestinationRule(string fixedDestination)
        {
            this.fixedDestination = fixedDestination;
        }

        public Task Invoke(SubscribePreroutingContext context, Func<SubscribePreroutingContext, Task> next)
        {
            context.Destinations.Add(new Destination(fixedDestination, null));
            return next(context);
        }
    }
}