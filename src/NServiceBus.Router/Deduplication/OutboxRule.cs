namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Transport;

    class OutboxRule : IRule<RawContext, RawContext>
    {
        IOutboxPersistence persistence;

        public OutboxRule(IOutboxPersistence persistence)
        {
            this.persistence = persistence;
        }

        public async Task Invoke(RawContext context, Func<RawContext, Task> next)
        {
            var capturedMessages = await persistence.GetUndispatchedOutgoingMessageses(context.MessageId).ConfigureAwait(false);
            if (capturedMessages == null)
            {
                capturedMessages = new List<CapturedOutgoingMessages>();
                context.Set(capturedMessages);

                await next(context).ConfigureAwait(false);

                context.Remove<List<CapturedOutgoingMessages>>();

                await persistence.Store(capturedMessages, context.Extensions.Get<TransportTransaction>()).ConfigureAwait(false);
            }

            var interfaces = context.Extensions.Get<IInterfaceChains>();

            var dispatchTasks = capturedMessages.Select(async m =>
            {
                var chains = interfaces.GetChainsFor(m.Interface);
                var postroutingChain = chains.Get<PostroutingContext>();
                var dispatchContext = new PostroutingContext(m.Operations, context);
                await postroutingChain.Invoke(dispatchContext).ConfigureAwait(false);
            });

            await Task.WhenAll(dispatchTasks).ConfigureAwait(false);
            await persistence.MarkDispatched(capturedMessages).ConfigureAwait(false);
        }
    }
}