using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Settings;
using NServiceBus.Transport;

class SendOnlyRawEndpointConfig<T> : IStartableRawEndpoint, IReceivingRawEndpoint
    where T : TransportDefinition, new()
{
    public SendOnlyRawEndpointConfig(string endpointName, Action<TransportExtensions<T>> transportCustomization)
    {
        config = RawEndpointConfiguration.CreateSendOnly(endpointName);
        var transport = config.UseTransport<T>();
        transportCustomization(transport);
    }

    public async Task<IStartableRawEndpoint> Create()
    {
        startable = await RawEndpoint.Create(config);
        config = null;
        return this;
    }

    async Task<IReceivingRawEndpoint> IStartableRawEndpoint.Start()
    {
        endpoint = await startable.Start().ConfigureAwait(false);
        startable = null;
        return this;
    }

    async Task<IStoppableRawEndpoint> IReceivingRawEndpoint.StopReceiving()
    {
        await transitionSemaphore.WaitAsync().ConfigureAwait(false);
        if (endpoint != null)
        {
            stoppable = await endpoint.StopReceiving().ConfigureAwait(false);
            endpoint = null;
        }
        return this;
    }

    async Task IStoppableRawEndpoint.Stop()
    {
        if (stoppable != null)
        {
            await stoppable.Stop().ConfigureAwait(false);
            stoppable = null;
        }
    }

    string IRawEndpoint.ToTransportAddress(LogicalAddress logicalAddress) => startable?.ToTransportAddress(logicalAddress) ?? endpoint.ToTransportAddress(logicalAddress);

    Task IDispatchMessages.Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, ContextBag context)
    {
        return endpoint != null
            ? endpoint.Dispatch(outgoingMessages, transaction, context)
            : startable.Dispatch(outgoingMessages, transaction, context);
    }

    string IRawEndpoint.TransportAddress => startable?.TransportAddress ?? endpoint.TransportAddress;
    string IRawEndpoint.EndpointName => startable?.EndpointName ?? endpoint.EndpointName;
    ReadOnlySettings IRawEndpoint.Settings => startable?.Settings ?? endpoint.Settings;
    public IManageSubscriptions SubscriptionManager => startable?.SubscriptionManager ?? endpoint.SubscriptionManager;

    RawEndpointConfiguration config;
    IReceivingRawEndpoint endpoint;
    IStartableRawEndpoint startable;
    SemaphoreSlim transitionSemaphore = new SemaphoreSlim(1);

    static ILog logger = LogManager.GetLogger(typeof(SendOnlyRawEndpointConfig<T>));
    IStoppableRawEndpoint stoppable;
}