using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Settings;
using NServiceBus.Transport;

interface Interface
{
    string Name { get; }
    Task Initialize(InterfaceChains interfaces, RootContext rootContext);
    Task StartReceiving();
    Task StopReceiving();
    Task Stop();
}

class Interface<T> : Interface where T : TransportDefinition, new()
{
    public string Name { get; }
    public Interface(string endpointName, string interfaceName, Action<TransportExtensions<T>> transportCustomization, Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory, string poisonQueue, int? maximumConcurrency, bool autoCreateQueues, string autoCreateQueuesIdentity, int immediateRetries, int delayedRetries, int circuitBreakerThreshold)
    {
        this.ruleCreationContextFactory = ruleCreationContextFactory;
        Name = interfaceName;
        rawConfig = new ThrottlingRawEndpointConfig<T>(endpointName, poisonQueue, ext =>
            {
                SetTransportSpecificFlags(ext.GetSettings(), poisonQueue);
                transportCustomization?.Invoke(ext);
            },
            (context, _) => preroutingChain.Invoke(new RawContext(context, Name, rootContext)),
            (context, dispatcher) =>
            {
                log.Error("Moving poison message to the error queue", context.Error.Exception);
                return context.MoveToErrorQueue(poisonQueue);
            },
            maximumConcurrency,
            immediateRetries, delayedRetries, circuitBreakerThreshold, autoCreateQueues, autoCreateQueuesIdentity);
    }

    static void SetTransportSpecificFlags(SettingsHolder settings, string poisonQueue)
    {
        settings.Set("errorQueue", poisonQueue);
        settings.Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
    }

    public async Task Initialize(InterfaceChains interfaces, RootContext rootContext)
    {
        this.rootContext = rootContext;
        sender = await rawConfig.Create().ConfigureAwait(false);
        var ruleCreationContext = ruleCreationContextFactory(sender);
        preroutingChain = interfaces.RegisterInterface(ruleCreationContext, Name, sender);
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

    static ILog log = LogManager.GetLogger(typeof(Interface));
    IReceivingRawEndpoint receiver;
    IStartableRawEndpoint sender;
    IStoppableRawEndpoint stoppable;

    ThrottlingRawEndpointConfig<T> rawConfig;
    IChain<RawContext> preroutingChain;
    Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory;
    RootContext rootContext;
}
