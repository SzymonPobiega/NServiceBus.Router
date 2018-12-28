using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Transport;

class RandomDuplicator : IRule<PostroutingContext, PostroutingContext>
{
    static Random random = new Random();

    public async Task Invoke(PostroutingContext context, Func<PostroutingContext, Task> next)
    {
        if (random.Next(2) == 0)
        {
            var copy = new PostroutingContext(context.DestinationEndpoint, Copy(context.Messages), context);
            await next(copy).ConfigureAwait(false);
        }
        await next(context).ConfigureAwait(false);
    }

    static TransportOperation[] Copy(TransportOperation[] operations)
    {
        return operations
            .Select(o => new TransportOperation(Copy(o.Message), o.AddressTag, o.RequiredDispatchConsistency, o.DeliveryConstraints))
            .ToArray();
    }

    static OutgoingMessage Copy(OutgoingMessage message)
    {
        return new OutgoingMessage(message.MessageId, new Dictionary<string, string>(message.Headers), message.Body);
    }
}