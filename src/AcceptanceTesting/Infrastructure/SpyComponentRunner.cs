using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Raw;
using NServiceBus.Transport;

class SpyComponentRunner : ComponentRunner
{
    Action<TransportExtensions<TestTransport>> transportConfiguration;
    Func<MessageContext, IDispatchMessages, Task> onMessage;
    string endpointName;
    IReceivingRawEndpoint endpoint;

    public SpyComponentRunner(string endpointName, Action<TransportExtensions<TestTransport>> transportConfiguration,
        Func<MessageContext, IDispatchMessages, Task> onMessage)
    {
        this.transportConfiguration = transportConfiguration;
        this.onMessage = onMessage;
        this.endpointName = endpointName;
    }

    public override string Name => endpointName;

    public override async Task Start(CancellationToken token)
    {
        var config = RawEndpointConfiguration.Create(endpointName, onMessage, "poison");
        config.AutoCreateQueue();
        config.CustomErrorHandlingPolicy(new IgnoreErrorsPolicy());
        var transport = config.UseTransport<TestTransport>();
        transportConfiguration(transport);

        endpoint = await RawEndpoint.Start(config);
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