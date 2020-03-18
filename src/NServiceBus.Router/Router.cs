namespace NServiceBus.Router
{
    using System;
    using System.Linq;

    /// <summary>
    /// Allows creating routers.
    /// </summary>
    public static class Router
    {
        /// <summary>
        /// Creates a new instance of a router based on the provided configuration.
        /// </summary>
        /// <param name="config">Router configuration.</param>
        public static IRouter Create(RouterConfiguration config)
        {
            if (config.RoutingProtocol == null)
            {
                throw new Exception("Routing protocol must be configured.");
            }

            config.EnableFeature(typeof(NativePubSubFeature));
            config.EnableFeature(typeof(MessageDrivenPubSubFeature));

            foreach (var featureType in config.Features)
            {
                var feature = (IFeature)Activator.CreateInstance(featureType);
                feature.Configure(config);
            }

            var sendOnlyInterfaces = config.SendOnlyInterfaceFactories.Select(x => x()).ToArray();
            var interfaces = config.InterfaceFactories.Select(x => x()).ToArray();
            var allInterfaceNames = sendOnlyInterfaces.Select(i => i.Name).Concat(interfaces.Select(i => i.Name)).ToArray();

            var chains = config.Chains;

            chains.AddChain(cb => cb.Begin<RawContext>().AddSection<PreroutingContext>().Terminate());
            chains.AddRule(_ => new RawToPreroutingConnector());
            chains.AddRule(c => new DetectCyclesRule(c.Endpoint.EndpointName));
            chains.AddRule(_ => new PreroutingTerminator());

            chains.AddRule(_ => new PreroutingToSubscribePreroutingFork());

            chains.AddChain(cb => cb.Begin<SubscribePreroutingContext>().Terminate());
            chains.AddRule(_ => new FindSubscribeDestinationsByHeadersRule());
            chains.AddRule(c => new SubscribePreroutingTerminator(config.RoutingProtocol, c.TypeGenerator));
            chains.AddChain(cb => cb.Begin<ForwardSubscribeContext>().Terminate());
            chains.AddRule(c => new ForwardSubscribeGatewayRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));

            chains.AddChain(cb => cb.Begin<UnsubscribePreroutingContext>().Terminate());
            chains.AddRule(_ => new FindUnsubscribeDestinationsByHeadersRule());
            chains.AddRule(c => new UnsubscribePreroutingTerminator(config.RoutingProtocol, c.TypeGenerator));
            chains.AddChain(cb => cb.Begin<ForwardUnsubscribeContext>().Terminate());
            chains.AddRule(c => new ForwardUnsubscribeGatewayRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));

            chains.AddRule(_ => new PreroutingToSendPreroutingFork());
            chains.AddChain(cb => cb.Begin<SendPreroutingContext>().Terminate());
            chains.AddRule(_ => new FindSendDestinationsByHeadersRule());
            chains.AddRule(_ => new SendPreroutingTerminator(config.RoutingProtocol));
            chains.AddChain(cb => cb.Begin<ForwardSendContext>().Terminate());
            chains.AddRule(c => new ForwardSendRule());
            chains.AddRule(c => new ForwardSendGatewayRule());

            chains.AddRule(_ => new PreroutingToPublishPreroutingFork());
            chains.AddChain(cb => cb.Begin<PublishPreroutingContext>().Terminate());
            chains.AddRule(c => new PublishPreroutingTerminator(allInterfaceNames, c.TypeGenerator));
            chains.AddChain(cb => cb.Begin<ForwardPublishContext>().Terminate());

            chains.AddRule(_ => new PreroutingToReplyPreroutingFork());
            chains.AddChain(cb => cb.Begin<ReplyPreroutingContext>().Terminate());
            chains.AddRule(_ => new ReplyPreroutingTerminator());
            chains.AddChain(cb => cb.Begin<ForwardReplyContext>().Terminate());
            chains.AddRule(_ => new ForwardReplyRule());
            chains.AddRule(c => new SendReplyTraceRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));
            chains.AddRule(c => new PublishReplyTraceRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));
            chains.AddRule(c => new ReplyReplyTraceRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));

            chains.AddChain(cb => cb.Begin<AnycastContext>().AddSection<PostroutingContext>().Terminate());
            chains.AddRule(c => new AnycastToPostroutingConnector(c.EndpointInstances, c.DistributionPolicy, instance => c.Endpoint.ToTransportAddress(LogicalAddress.CreateRemoteAddress(instance))));
            chains.AddChain(cb => cb.Begin<MulticastContext>().AddSection<PostroutingContext>().Terminate());
            chains.AddRule(c => new MulticastToPostroutingConnector(c.EndpointInstances, instance => c.Endpoint.ToTransportAddress(LogicalAddress.CreateRemoteAddress(instance))));
            chains.AddChain(cb => cb.Begin<PostroutingContext>().Terminate());
            chains.AddRule(c => new PostroutingTerminator(c.Endpoint));

            return new RouterImpl(config.Name, interfaces, sendOnlyInterfaces, config.Modules.ToArray(), config.RoutingProtocol, chains, config.Settings);
        }
    }
}