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

        if (!context.Headers.TryGetValue(RouterHeaders.ReplyToTrace, out var replyToTrace)
         && !context.Headers.TryGetValue(Headers.CorrelationId, out replyToTrace))
        {
            throw new UnforwardableMessageException($"The reply has to contain either '{Headers.CorrelationId}' header set by the sending endpoint or '{RouterHeaders.ReplyToTrace}' set by the replying endpoint in order to be routed.");
        }
        try
        {
            replyToTrace.DecodeTLV((t, v) =>
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