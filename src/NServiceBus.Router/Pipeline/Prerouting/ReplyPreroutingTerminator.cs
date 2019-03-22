using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class ReplyPreroutingTerminator : ChainTerminator<ReplyPreroutingContext>
{
    protected override async Task<bool> Terminate(ReplyPreroutingContext context)
    {
        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var iface = InterfaceForReply(context);
        var chain = interfaces.GetChainsFor(iface);
        var forkContext = new ForwardReplyContext(iface, context);

        await chain.Get<ForwardReplyContext>().Invoke(forkContext).ConfigureAwait(false);

        return true;
    }

    static string InterfaceForReply(ReplyPreroutingContext context)
    {
        string destinationIface = null;

        if (!context.Headers.TryGetValue(RouterHeaders.PreviousCorrelationId, out var correlationId)
         && !context.Headers.TryGetValue(Headers.CorrelationId, out correlationId))
        {
            throw new UnforwardableMessageException($"The reply has to contain a '{Headers.CorrelationId}' header set by the sending endpoint when sending out the initial message.");
        }
        try
        {
            correlationId.DecodeTLV((t, v) =>
            {
                if (t == "iface" || t == "port") //Port for compat reasons
                {
                    destinationIface = v;
                }
            });
        }
        catch (Exception e)
        {
            throw new UnforwardableMessageException($"Cannot decode value in \'{Headers.CorrelationId}\' header: {e.Message}");
        }

        if (destinationIface == null)
        {
            throw new UnforwardableMessageException("The reply message does not contain \'iface\' correlation parameter required to route the message.");
        }
        return destinationIface;
    }
}