using System;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;

class ForwardPublishNativeRule : IRule<ForwardPublishContext, ForwardPublishContext>
{
    RuntimeTypeGenerator typeGenerator;

    public ForwardPublishNativeRule(RuntimeTypeGenerator typeGenerator)
    {
        this.typeGenerator = typeGenerator;
    }

    public async Task Invoke(ForwardPublishContext context, Func<ForwardPublishContext, Task> next)
    {
        var addressTag = new MulticastAddressTag(typeGenerator.GetType(context.Types.First()));
        var outgoingMessage = new OutgoingMessage(context.MessageId, context.ReceivedHeaders.Copy(), context.ReceivedBody);
        var operation = new TransportOperation(outgoingMessage, addressTag);

        var forkContext = new PostroutingContext(operation, context);
        var chain = context.Chains.Get<PostroutingContext>();
        await chain.Invoke(forkContext).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}