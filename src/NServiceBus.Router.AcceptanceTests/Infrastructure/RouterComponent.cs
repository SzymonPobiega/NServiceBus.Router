using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Support;

namespace NServiceBus.Router.AcceptanceTests
{
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
            config.Settings.Set<ScenarioContext>(run.ScenarioContext);
            config.AutoCreateQueues();
            var newFactories = new List<Func<Interface>>();

            foreach (var factory in config.InterfaceFactories)
            {
                Interface NewFactory()
                {
                    var port = factory();
                    return port;
                }

                newFactories.Add(NewFactory);
            }

            config.InterfaceFactories = newFactories;
            var @switch = Router.Create(config);
            return Task.FromResult<ComponentRunner>(new Runner(@switch, "Router"));
        }

        class Runner : ComponentRunner
        {
            IRouter router;

            public Runner(IRouter router, string name)
            {
                this.router = router;
                Name = name;
            }

            public override Task Start(CancellationToken token)
            {
                return router.Initialize();
            }

            public override Task ComponentsStarted(CancellationToken token)
            {
                return router.Start();
            }

            public override Task Stop()
            {
                return router != null
                    ? router.Stop()
                    : Task.CompletedTask;
            }

            public override string Name { get; }
        }
    }
}