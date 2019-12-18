namespace NServiceBus.Router.Migrator
{
    using System;
    using System.Threading.Tasks;
    using Configuration.AdvancedExtensibility;
    using Features;
    using Routing;
    using Transport;
    using Unicast.Messages;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    /// <summary>
    /// Configures migrator
    /// </summary>
    public static class MigratorConfigurationExtensions
    {
        internal const string NewTransportCustomizationSettingsKey = "NServiceBus.Router.Migrator.NewTransportCustomization";
        internal const string OldTransportCustomizationSettingsKey = "NServiceBus.Router.Migrator.OldTransportCustomization";

        /// <summary>
        /// Enables transport migration for this endpoint.
        /// </summary>
        /// <typeparam name="TOld">Old transport</typeparam>
        /// <typeparam name="TNew">New transport</typeparam>
        /// <param name="endpointConfiguration">Endpoint configuration</param>
        /// <param name="customizeOldTransport">A callback for customizing the old transport</param>
        /// <param name="customizeNewTransport">A callback for customizing the new transport</param>
        public static void EnableTransportMigration<TOld, TNew>(this EndpointConfiguration endpointConfiguration,
            Action<TransportExtensions<TOld>> customizeOldTransport, Action<TransportExtensions<TNew>> customizeNewTransport)
        where TOld : TransportDefinition, new()
        where TNew : TransportDefinition, new()
        {
            //TODO: Installers

            var endpointTransport = endpointConfiguration.UseTransport<TNew>();
            customizeNewTransport(endpointTransport);

            endpointConfiguration.GetSettings().Set(NewTransportCustomizationSettingsKey, customizeNewTransport);
            endpointConfiguration.GetSettings().Set(OldTransportCustomizationSettingsKey, customizeOldTransport);

            endpointConfiguration.EnableFeature<MigratorFeature<TOld, TNew>>();
        }
    }

    class MigratorFeature<TOld, TNew> : Feature
      where TOld : TransportDefinition, new()
      where TNew : TransportDefinition, new()
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var mainEndpointName = context.Settings.EndpointName();
            var routerEndpointName = $"{mainEndpointName}_Migrator";

            var transportInfrastructure = context.Settings.Get<TransportInfrastructure>();

            var customizeOldTransport = context.Settings.Get<Action<TransportExtensions<TOld>>>(MigratorConfigurationExtensions.OldTransportCustomizationSettingsKey);
            var customizeNewTransport = context.Settings.Get<Action<TransportExtensions<TNew>>>(MigratorConfigurationExtensions.NewTransportCustomizationSettingsKey);

            var routerAddress = transportInfrastructure.ToTransportAddress(LogicalAddress.CreateRemoteAddress(new EndpointInstance(routerEndpointName)));

            context.Pipeline.Register(b => new PublishRedirectionBehavior(routerAddress, b.Build<ISubscriptionStorage>(), b.Build<MessageMetadataRegistry>()),
                "Redirects publishes that target old transport address via the router");

            context.Pipeline.Register(b => new UnsubscribeWhenMigratedBehavior(b.Build<ISubscriptionStorage>()),
                "Removes old transport subscriptions when a new transport subscription for the same event and endpoint comes in");

            context.Pipeline.Register(new IgnoreDuplicatesBehavior(mainEndpointName), "Ignores duplicates when publishing both natively and message-driven");

            var routerConfig = PrepareRouterConfiguration(routerEndpointName, mainEndpointName, context.Settings.LocalAddress(), customizeOldTransport, customizeNewTransport);
            context.RegisterStartupTask(new MigratorStartupTask(routerConfig));
        }

        RouterConfiguration PrepareRouterConfiguration(string routerEndpointName, string mainEndpointName, string mainEndpointAddress, Action<TransportExtensions<TOld>> customizeOldTransport, Action<TransportExtensions<TNew>> customizeNewTransport)
        {
            var cfg = new RouterConfiguration(routerEndpointName);

            var bravoInterface = cfg.AddInterface("New", customizeNewTransport);
            bravoInterface.DisableMessageDrivenPublishSubscribe();

            //Forward unmodified subscribe messages from migrated subscriber
            bravoInterface.AddRule(c => new ForwardSubscribeUnmodifiedRule());

            //Forward published events from shadow interface to migrated subscriber
            bravoInterface.AddRule(c => new ShadowForwardPublishRule(mainEndpointAddress));

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

            var staticRouting = cfg.UseStaticRoutingProtocol();
            staticRouting.AddForwardRoute("New", "Shadow");
            staticRouting.AddForwardRoute("Shadow", "New");

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
