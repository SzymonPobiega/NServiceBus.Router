using System;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Router;

public static class RouterComponentExtensions
{
    public static IScenarioWithEndpointBehavior<TContext> WithRouter<TContext>(this IScenarioWithEndpointBehavior<TContext> scenario, string name, Action<RouterConfiguration> configCallback)
        where TContext : ScenarioContext
    {
        return scenario.WithComponent(new RouterComponent(s =>
        {
            var cfg = new RouterConfiguration(name);
            configCallback(cfg);
            return cfg;
        }));
    }

    public static IScenarioWithEndpointBehavior<TContext> WithRouter<TContext>(this IScenarioWithEndpointBehavior<TContext> scenario, string name, Action<TContext, RouterConfiguration> configCallback)
        where TContext : ScenarioContext
    {
        return scenario.WithComponent(new RouterComponent(s =>
        {
            var cfg = new RouterConfiguration(name);
            configCallback((TContext)s, cfg);
            return cfg;
        }));
    }
}
