using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;

public static class SpyComponentExtensions
{
    public static IScenarioWithEndpointBehavior<TContext> WithPosionSpyComponent<TContext>(this IScenarioWithEndpointBehavior<TContext> scenario, Action<TransportExtensions<TestTransport>> transportConfiguration)
        where TContext : ScenarioContext, IPoisonSpyContext
    {
        return scenario.WithComponent(new SpyComponent<TContext>("poison", transportConfiguration, (scenarioContext, messageContext, dispatcher) =>
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
        Action<TransportExtensions<TestTransport>> transportConfiguration,
        Func<TContext, MessageContext, IDispatchMessages, Task> onMessage)
        where TContext : ScenarioContext
    {
        return scenario.WithComponent(new SpyComponent<TContext>(endpointName, transportConfiguration, onMessage));
    }
}
