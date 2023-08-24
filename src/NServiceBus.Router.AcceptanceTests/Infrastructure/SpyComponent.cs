using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;

namespace NServiceBus.Router.AcceptanceTests
{
    class SpyComponent<T> : IComponentBehavior
        where T : ScenarioContext
    {
        TransportDefinition transportConfiguration;
        Func<T, MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage;
        string endpointName;

        public SpyComponent(string endpointName, TransportDefinition transportConfiguration,
            Func<T, MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage)
        {
            this.transportConfiguration = transportConfiguration;
            this.onMessage = onMessage;
            this.endpointName = endpointName;
        }

        public Task<ComponentRunner> CreateRunner(RunDescriptor run)
        {
            var scenarioContext = (T)run.ScenarioContext;

            return Task.FromResult<ComponentRunner>(new SpyComponentRunner(endpointName, transportConfiguration, (messageContext, messages, token) => onMessage(scenarioContext, messageContext, messages, token)));
        }
    }
}