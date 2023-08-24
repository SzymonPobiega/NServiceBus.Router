using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Transport;


class SendOnlyInterface
{
    public SendOnlyInterface(string endpointName, string interfaceName, TransportDefinition transport, Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory)
    {
        this.ruleCreationContextFactory = ruleCreationContextFactory;
        Name = interfaceName;

        config = RawEndpointConfiguration.CreateSendOnly(endpointName, transport);
    }

    public string Name { get; }

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
