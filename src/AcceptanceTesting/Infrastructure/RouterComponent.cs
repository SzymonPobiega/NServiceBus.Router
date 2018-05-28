using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Router;


class RouterComponent : IComponentBehavior
{
    Func<ScenarioContext, RouterConfiguration> configCallback;

    public RouterComponent(Func<ScenarioContext, RouterConfiguration> config)
    {
        this.configCallback = config;
    }

    public Task<ComponentRunner> CreateRunner(RunDescriptor run)
    {
        var config = configCallback(run.ScenarioContext);

        config.AutoCreateQueues();
        var newFactories = new List<Func<Interface>>();

        foreach (var factory in config.PortFactories)
        {
            Interface NewFactory()
            {
                var port = factory();
                return port;
            }
            newFactories.Add(NewFactory);
        }

        config.PortFactories = newFactories;
        var @switch = Router.Create(config);
        return Task.FromResult<ComponentRunner>(new Runner(@switch, "Router"));
    }

    class Runner : ComponentRunner
    {
        IRouter @switch;

        public Runner(IRouter @switch, string name)
        {
            this.@switch = @switch;
            Name = name;
        }

        public override Task Start(CancellationToken token)
        {
            return @switch.Start();
        }

        public override Task Stop()
        {
            return @switch != null 
                ? @switch.Stop() 
                : Task.CompletedTask;
        }

        public override string Name { get; }
    }
}