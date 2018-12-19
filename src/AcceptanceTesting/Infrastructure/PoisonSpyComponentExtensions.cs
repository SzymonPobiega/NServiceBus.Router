using System;
using NServiceBus;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

public static class PoisonSpyComponentExtensions
{
    public static IScenarioWithEndpointBehavior<TContext> WithPosionSpyComponent<TContext>(this IScenarioWithEndpointBehavior<TContext> scenario, Action<TransportExtensions<TestTransport>> transportConfiguration)
        where TContext : ScenarioContext
    {
        return scenario.WithComponent(new PoisonSpyComponent(transportConfiguration));
    }
}