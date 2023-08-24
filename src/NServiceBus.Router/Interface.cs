﻿using System;
using System.Threading.Tasks;
using NServiceBus.Router;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Transport;

class Interface
{
    public string Name { get; }
    public Interface(string endpointName, string interfaceName, TransportDefinition transport, Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory, string poisonQueue, int? maximumConcurrency, bool autoCreateQueues, int immediateRetries, int delayedRetries, int circuitBreakerThreshold)
    {
        this.ruleCreationContextFactory = ruleCreationContextFactory;
        Name = interfaceName;
        rawConfig = new ThrottlingRawEndpointConfig(endpointName, poisonQueue, transport,
            (context, token, _) => preroutingChain.Invoke(new RawContext(context, Name, rootContext)),
            (context, dispatcher) =>
            {
                log.Error("Moving poison message to the error queue", context.Error.Exception);
                return context.MoveToErrorQueue(poisonQueue);
            },
            maximumConcurrency,
            immediateRetries, delayedRetries, circuitBreakerThreshold, autoCreateQueues);
    }

    public async Task Initialize(InterfaceChains interfaces, RootContext rootContext)
    {
        this.rootContext = rootContext;
        sender = await rawConfig.Create().ConfigureAwait(false);
        var ruleCreationContext = ruleCreationContextFactory(sender);
        interfaces.InitializeInterface(Name, ruleCreationContext);
        preroutingChain = interfaces.GetChainsFor(Name).Get<RawContext>();
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

    ThrottlingRawEndpointConfig rawConfig;
    IChain<RawContext> preroutingChain;
    Func<IRawEndpoint, IRuleCreationContext> ruleCreationContextFactory;
    RootContext rootContext;
}
