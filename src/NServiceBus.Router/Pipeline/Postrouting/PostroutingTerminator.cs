using System.Threading.Tasks;
using NServiceBus.Raw;
using NServiceBus.Router;
using NServiceBus.Transport;

class PostroutingTerminator : ChainTerminator<PostroutingContext>
{
    IRawEndpoint dispatcher;
    string endpointName;

    public PostroutingTerminator(IRawEndpoint dispatcher)
    {
        this.dispatcher = dispatcher;
        this.endpointName = dispatcher.EndpointName;
    }

    protected override async Task<bool> Terminate(PostroutingContext context)
    {
        foreach (var operation in context.Messages)
        {
            AddTrace(operation);
        }
        await dispatcher.Dispatch(new TransportOperations(context.Messages), context.Get<TransportTransaction>(), context)
            .ConfigureAwait(false);
        return true;
    }

    void AddTrace(TransportOperation op)
    {
        if (op.Message.Headers.TryGetValue("NServiceBus.Bridge.Trace", out var trace))
        {
            trace = trace.AppendTLV("via", endpointName);
        }
        else
        {
            trace = TLV.Encode("via", endpointName);
        }
        op.Message.Headers["NServiceBus.Bridge.Trace"] = trace;
    }
}
