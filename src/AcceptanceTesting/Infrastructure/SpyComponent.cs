using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;

class SpyComponent<T> : IComponentBehavior
    where T : ScenarioContext
{
    Action<TransportExtensions<TestTransport>> transportConfiguration;
    Func<T, MessageContext, IDispatchMessages, Task> onMessage;
    string endpointName;

    public SpyComponent(string endpointName, Action<TransportExtensions<TestTransport>> transportConfiguration,
        Func<T, MessageContext, IDispatchMessages, Task> onMessage)
    {
        this.transportConfiguration = transportConfiguration;
        this.onMessage = onMessage;
        this.endpointName = endpointName;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var scenarioContext = (T)run.ScenarioContext;

        return Task.FromResult<ComponentRunner>(new SpyComponentRunner(endpointName, transportConfiguration, (messageContext, messages) => onMessage(scenarioContext, messageContext, messages), scenarioContext));
    }
}