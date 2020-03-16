using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Raw;
using NServiceBus.Transport;

class SendOnlyInterface<T> : Interface where T : TransportDefinition, new()
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

    public async Task Initialize(InterfaceChains interfaces, RootContext rootContext)
    {
        startable = await RawEndpoint.Create(config).ConfigureAwait(false);
        config = null;
        var ruleCreationContext = ruleCreationContextFactory(startable);
        interfaces.InitializeInterface(Name, ruleCreationContext);
    }

    public async Task StartReceiving()
    {
        receiver = await startable.Start().ConfigureAwait(false);
    }

    public async Task StopReceiving()
    {
        if (receiver != null)
        {
            stoppable = await receiver.StopReceiving().ConfigureAwait(false);
        }
        else
        {
            stoppable = null;
        }
    }

    public async Task Stop()
    {
        if (stoppable != null)
        {
            await stoppable.Stop().ConfigureAwait(false);
            stoppable = null;
        }
    }

    RawEndpointConfiguration config;
    IStartableRawEndpoint startable;
    IReceivingRawEndpoint receiver;
    IStoppableRawEndpoint stoppable;

    Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory;
}
