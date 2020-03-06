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
        rawConfig = new SendOnlyRawEndpointConfig<T>(endpointName, ext =>
        {
            SetTransportSpecificFlags(ext.GetSettings());
            transportCustomization?.Invoke(ext);
        });
    }

    public string Name { get; }

    static void SetTransportSpecificFlags(NServiceBus.Settings.SettingsHolder settings)
    {
        settings.Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
    }

    public async Task Initialize(InterfaceChains interfaces, RootContext rootContext)
    {
        sender = await rawConfig.Create().ConfigureAwait(false);
        var ruleCreationContext = ruleCreationContextFactory(sender);
        interfaces.InitializeInterface(Name, ruleCreationContext);
    }

    public async Task StartReceiving()
    {
        receiver = await sender.Start().ConfigureAwait(false);
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

    IReceivingRawEndpoint receiver;
    IStartableRawEndpoint sender;
    IStoppableRawEndpoint stoppable;

    SendOnlyRawEndpointConfig<T> rawConfig;
    Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory;
}
