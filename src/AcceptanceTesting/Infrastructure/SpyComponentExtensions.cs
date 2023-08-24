using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;

public static class SpyComponentExtensions
{
    public static IScenarioWithEndpointBehavior<TContext> WithPosionSpyComponent<TContext>(this IScenarioWithEndpointBehavior<TContext> scenario, TransportDefinition transportConfiguration)
        where TContext : ScenarioContext, IPoisonSpyContext
    {
        return scenario.WithComponent(new SpyComponent<TContext>("poison", transportConfiguration, (scenarioContext, messageContext, dispatcher, token) =>
        {
            var failureDetected = 0;

            if (Interlocked.CompareExchange(ref failureDetected, 1, 0) == 0)
            {
                if (messageContext.Headers.TryGetValue("NServiceBus.ExceptionInfo.Message", out var exceptionMessage))
                {
                    scenarioContext.ExceptionMessage = exceptionMessage;
                }
                scenarioContext.PoisonMessageDetected = true;
            }
            return Task.CompletedTask;
        }));
    }

    public static IScenarioWithEndpointBehavior<TContext> WithSpyComponent<TContext>(this IScenarioWithEndpointBehavior<TContext> scenario,
        string endpointName,
        TransportDefinition transportConfiguration,
        Func<TContext, MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage)
        where TContext : ScenarioContext
    {
        return scenario.WithComponent(new SpyComponent<TContext>(endpointName, transportConfiguration, onMessage));
    }
}
