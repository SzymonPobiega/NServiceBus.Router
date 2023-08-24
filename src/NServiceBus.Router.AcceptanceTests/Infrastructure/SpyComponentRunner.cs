using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Raw;
using NServiceBus.Transport;

namespace NServiceBus.Router.AcceptanceTests
{
    class SpyComponentRunner : ComponentRunner
    {
        TransportDefinition transportConfiguration;
        Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage;
        string endpointName;
        IReceivingRawEndpoint endpoint;

        public SpyComponentRunner(string endpointName, TransportDefinition transportConfiguration,
            Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage)
        {
            this.transportConfiguration = transportConfiguration;
            this.onMessage = onMessage;
            this.endpointName = endpointName;
        }

        public override string Name => endpointName;

        public override async Task Start(CancellationToken token)
        {
            var config = RawEndpointConfiguration.Create(endpointName, transportConfiguration, onMessage, "poison");
            config.AutoCreateQueues();
            config.CustomErrorHandlingPolicy(new IgnoreErrorsPolicy());

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
            public Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IMessageDispatcher dispatcher, CancellationToken token)
            {
                return Task.FromResult(ErrorHandleResult.Handled);
            }
        }
    }
}