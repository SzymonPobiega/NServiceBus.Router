namespace NServiceBus.Router
{
    using System.Threading.Tasks;
    using Raw;
    using Transport;

    class PostroutingTerminator : ChainTerminator<PostroutingContext>
    {
        IRawEndpoint dispatcher;
        string endpointName;

        public PostroutingTerminator(IRawEndpoint dispatcher)
        {
            this.dispatcher = dispatcher;
            this.endpointName = dispatcher.EndpointName;
        }

        protected override Task Terminate(PostroutingContext context)
        {
            foreach (var operation in context.Messages.UnicastTransportOperations)
            {
                AddTrace(operation);
            }
            foreach (var operation in context.Messages.MulticastTransportOperations)
            {
                AddTrace(operation);
            }
            return dispatcher.Dispatch(context.Messages, context.Get<TransportTransaction>(), context);
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
    }
}