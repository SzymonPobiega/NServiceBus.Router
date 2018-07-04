using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Raw;
using NServiceBus.Transport;

class PoisonSpyComponent : IComponentBehavior
{
    Action<TransportExtensions<TestTransport>> transportConfiguration;

    public PoisonSpyComponent(Action<TransportExtensions<TestTransport>> transportConfiguration)
    {
        this.transportConfiguration = transportConfiguration;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        return Task.FromResult<ComponentRunner>(new Runner(transportConfiguration, (IPoisonSpyContext)run.ScenarioContext));
    }

    class Runner : ComponentRunner
    {
        Action<TransportExtensions<TestTransport>> transportConfiguration;
        IPoisonSpyContext scenarioContext;
        IReceivingRawEndpoint endpoint;
        int failureDetected;

        public Runner(Action<TransportExtensions<TestTransport>> transportConfiguration, IPoisonSpyContext scenarioContext)
        {
            this.transportConfiguration = transportConfiguration;
            this.scenarioContext = scenarioContext;
        }

        public override string Name => "PoisonQueueSpy";

        public override async Task Start(CancellationToken token)
        {
            var config = RawEndpointConfiguration.Create("poison", OnMessage, "poison");
            config.AutoCreateQueue();
            config.CustomErrorHandlingPolicy(new IgnoreErrorsPolicy());
            var transport = config.UseTransport<TestTransport>();
            transportConfiguration(transport);

            endpoint = await RawEndpoint.Start(config);
        }

        Task OnMessage(MessageContext messageContext, IDispatchMessages dispatcher)
        {
            if (Interlocked.CompareExchange(ref failureDetected, 1, 0) == 0)
            {
                if (messageContext.Headers.TryGetValue("NServiceBus.ExceptionInfo.Message", out var exceptionMessage))
                {
                    scenarioContext.ExceptionMessage = exceptionMessage;
                }
                scenarioContext.PoisonMessageDetected = true;
            }
            return Task.CompletedTask;
        }

        public override Task Stop()
        {
            return endpoint != null
                ? endpoint.Stop()
                : Task.CompletedTask;
        }

        class IgnoreErrorsPolicy : IErrorHandlingPolicy
        {
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IDispatchMessages dispatcher)
            {
                return Task.FromResult(ErrorHandleResult.Handled);
            }
        }
    }
}