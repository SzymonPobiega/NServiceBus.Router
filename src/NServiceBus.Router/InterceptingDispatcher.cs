using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Extensibility;
using NServiceBus.Raw;
using NServiceBus.Settings;
using NServiceBus.Transport;

class InterceptingDispatcher : IRawEndpoint
{
    IRawEndpoint impl;
    Dispatch dispatchImpl;
    string endpointName;

    public InterceptingDispatcher(IRawEndpoint impl, Dispatch dispatchImpl, string endpointName)
    {
        this.impl = impl;
        this.dispatchImpl = dispatchImpl;
        this.endpointName = endpointName;
    }

    public Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
    {
        foreach (var op in outgoingMessages.UnicastTransportOperations)
        {
            AddTrace(op);
        }
        foreach (var op in outgoingMessages.MulticastTransportOperations)
        {
            AddTrace(op);
        }
        return dispatchImpl(outgoingMessages, transaction, context);
    }

    void AddTrace(IOutgoingTransportOperation op)
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

    public string ToTransportAddress(LogicalAddress logicalAddress) => impl.ToTransportAddress(logicalAddress);

    public string TransportAddress => impl.TransportAddress;

    public string EndpointName => impl.EndpointName;

    public ReadOnlySettings Settings => impl.Settings;
    public IManageSubscriptions SubscriptionManager => impl.SubscriptionManager;
}