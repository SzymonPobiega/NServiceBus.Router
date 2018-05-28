namespace NServiceBus.Router
{
    using System;
    using System.Threading.Tasks;
    using Extensibility;
    using Transport;

    /// <summary>
    /// Allows to execute arbitrary code before forwarding a message.
    /// </summary>
    public delegate Task InterceptMessageForwarding(string inputQueue, MessageContext message, Dispatch dispatchMethod, Func<Dispatch, Task> forwardMethod);

    /// <summary>
    /// Represents a call to message dispatcher.
    /// </summary>
    public delegate Task Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context);
}