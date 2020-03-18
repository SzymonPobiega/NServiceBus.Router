using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Raw;
using NServiceBus.Transport;

interface SendOnlyInterface
{
    string Name { get; }
    Task Initialize(InterfaceChains interfaces);
    Task Stop();
}

class SendOnlyInterface<T> : SendOnlyInterface where T : TransportDefinition, new()
{
    public SendOnlyInterface(string endpointName, string interfaceName, Action<TransportExtensions<T>> transportCustomization, Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory)
    {
        this.ruleCreationContextFactory = ruleCreationContextFactory;
        Name = interfaceName;

        config = RawEndpointConfiguration.CreateSendOnly(endpointName);
        var transport = config.UseTransport<T>();
        SetTransportSpecificFlags(transport.GetSettings());
        transportCustomization?.Invoke(transport);
    }

    public string Name { get; }

    static void SetTransportSpecificFlags(NServiceBus.Settings.SettingsHolder settings)
    {
        settings.Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
    }

    public async Task Initialize(InterfaceChains interfaces)
    {
        var startable = await RawEndpoint.Create(config).ConfigureAwait(false);
        config = null;
        var ruleCreationContext = ruleCreationContextFactory(startable);
        interfaces.InitializeInterface(Name, ruleCreationContext);

        endpoint = await startable.Start().ConfigureAwait(false);
    }

    public async Task Stop()
    {
        if (endpoint != null)
        {
            await endpoint.Stop().ConfigureAwait(false);
            endpoint = null;
        }
    }

    RawEndpointConfiguration config;
    IStoppableRawEndpoint endpoint;

    Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory;
}
