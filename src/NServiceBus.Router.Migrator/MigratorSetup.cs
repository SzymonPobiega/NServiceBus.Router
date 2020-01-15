namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Features;
    using Routing;
    using Transport;
    using Unicast.Messages;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    class MigratorFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var setupInstance = (IMigratorSetup)context.Settings.Get("NServiceBus.Router.Migrator.Setup");
            setupInstance.Setup(context);
        }
    }

    interface IMigratorSetup
    {
        void Setup(FeatureConfigurationContext context);
    }

    class MigratorSetup<TOld, TNew> : IMigratorSetup
        where TOld : TransportDefinition, new()
        where TNew : TransportDefinition, new()
    {
        public void Setup(FeatureConfigurationContext context)
        {
            var mainEndpointName = context.Settings.EndpointName();
            var routerEndpointName = $"{mainEndpointName}_Migrator";
            var settings = context.Settings.Get<MigratorSettings>();
            var unicastRouteTable = context.Settings.Get<UnicastRoutingTable>();
            var distributionPolicy = context.Settings.Get<DistributionPolicy>();
            var transportInfrastructure = context.Settings.Get<TransportInfrastructure>();
            var queueBindings = context.Settings.Get<QueueBindings>();

            var customizeOldTransport = context.Settings.Get<Action<TransportExtensions<TOld>>>(MigratorConfigurationExtensions.OldTransportCustomizationSettingsKey);
            var customizeNewTransport = context.Settings.Get<Action<TransportExtensions<TNew>>>(MigratorConfigurationExtensions.NewTransportCustomizationSettingsKey);

            var routerAddress = transportInfrastructure.ToTransportAddress(LogicalAddress.CreateRemoteAddress(new EndpointInstance(routerEndpointName)));
            queueBindings.BindSending(routerAddress);


            //context.RegisterStartupTask(b => new RoutingMonitor(unicastRouteTable, settings.SendRouteTable, route, b.Build<CriticalError>()));

            var route = UnicastRoute.CreateFromPhysicalAddress(routerAddress);
            var routes = settings.SendRouteTable.Select(x => new RouteTableEntry(x.Key, route)).ToList();
            unicastRouteTable.AddOrReplaceRoutes("NServiceBus.Router.Migrator", routes);

            context.Pipeline.Register(new MigratorRouterDestinationBehavior(settings.SendRouteTable),
                "Sets the ultimate destination endpoint on the outgoing messages.");

            context.Pipeline.Replace("MigrationModePublishConnector", b => new DualRoutingPublishConnector(routerAddress, distributionPolicy, b.Build<MessageMetadataRegistry>(), i => transportInfrastructure.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)), b.Build<ISubscriptionStorage>()),
                "Routes published messages via router and publishes them natively");

            context.Pipeline.Register(b => new UnsubscribeAfterMigrationBehavior(b.BuildAll<ISubscriptionStorage>().FirstOrDefault()),
                "Removes old transport subscriptions when a new transport subscription for the same event and endpoint comes in");

            context.Pipeline.Register(new DualRoutingFilterBehavior(mainEndpointName), "Ignores duplicates when publishing both natively and message-driven");

            context.Pipeline.Register(b => new MigratorRouterSubscribeBehavior(context.Settings.LocalAddress(), context.Settings.EndpointName(), routerAddress, b.Build<IDispatchMessages>(), settings.PublisherTable),
                "Dispatches the subscribe request via a router.");

            var routerConfig = PrepareRouterConfiguration(routerEndpointName, mainEndpointName, context.Settings.LocalAddress(), customizeOldTransport, customizeNewTransport);
            context.RegisterStartupTask(new MigratorStartupTask(routerConfig));
        }

        RouterConfiguration PrepareRouterConfiguration(string routerEndpointName, string mainEndpointName, string mainEndpointAddress, Action<TransportExtensions<TOld>> customizeOldTransport, Action<TransportExtensions<TNew>> customizeNewTransport)
        {
            var cfg = new RouterConfiguration(routerEndpointName);

            var newInterface = cfg.AddInterface("New", customizeNewTransport);
            newInterface.DisableNativePubSub();

            //Forward unmodified subscribe messages from migrated subscriber
            newInterface.AddRule(c => new ForwardSubscribeRule());

            //Forward published events from shadow interface to migrated subscriber
            newInterface.AddRule(c => new ForwardPublishRule(mainEndpointAddress));


            var shadowInterface = cfg.AddInterface("Shadow", customizeOldTransport);
            shadowInterface.DisableMessageDrivenPublishSubscribe();

            //Hook up to old Publisher's queue
            shadowInterface.OverrideEndpointName(mainEndpointName);

            //Forward subscribe messages
            shadowInterface.AddRule(c => new ShadowForwardSubscribeRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));

            //Forward events published by migrated publisher
            shadowInterface.AddRule(c => new ForwardPublishByDestinationAddressRule());

            //Forward subscribes messages from shadow interface to migrated publisher
            shadowInterface.AddRule(c => new ShadowSubscribeDestinationRule(mainEndpointName));

            //Forward sends from shadow interface to migrated receiver
            shadowInterface.AddRule(c => new ShadowSendDestinationRule(mainEndpointName));

            //Removes the destination header
            shadowInterface.AddRule(_ => new ForwardSendRule());

            var staticRouting = cfg.UseStaticRoutingProtocol();
            staticRouting.AddForwardRoute("New", "Shadow");
            staticRouting.AddForwardRoute("Shadow", "New");

            cfg.AutoCreateQueues(null);

            return cfg;
        }

        class MigratorStartupTask : FeatureStartupTask
        {
            IRouter router;

            public MigratorStartupTask(RouterConfiguration routerConfiguration)
            {
                router = Router.Create(routerConfiguration);
            }

            protected override Task OnStart(IMessageSession session)
            {
                return router.Start();
            }

            protected override Task OnStop(IMessageSession session)
            {
                return router.Stop();
            }
        }
    }
}