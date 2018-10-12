using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;

class ReplyPreroutingTerminator : ChainTerminator<ReplyPreroutingContext>
{
    protected override Task Terminate(ReplyPreroutingContext context)
    {
        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var iface = InterfaceForReply(context);
        var chain = interfaces.GetChainsFor(iface);
        var forkContext = new ForwardReplyContext(iface, context);

        return chain.Get<ForwardReplyContext>().Invoke(forkContext);
    }

    static string InterfaceForReply(ReplyPreroutingContext context)
    {
        string destinationIface = null;
        if (!context.Headers.TryGetValue(Headers.CorrelationId, out var correlationId))
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