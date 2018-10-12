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

            var interfaces = config.InterfaceFactories.Select(x => x()).ToArray();

            var chains = config.Chains;

            chains.AddChain(cb => cb.Begin<RawContext>().AddSection<PreroutingContext>().Terminate());
            chains.AddChain(cb => cb.Begin<SubscribePreroutingContext>().Terminate());
            chains.AddChain(cb => cb.Begin<UnsubscribePreroutingContext>().Terminate());
            chains.AddChain(cb => cb.Begin<SendPreroutingContext>().Terminate());
            chains.AddChain(cb => cb.Begin<PublishPreroutingContext>().Terminate());
            chains.AddChain(cb => cb.Begin<ReplyPreroutingContext>().Terminate());

            chains.AddChain(cb => cb.Begin<ForwardSubscribeContext>().Terminate());
            chains.AddChain(cb => cb.Begin<ForwardUnsubscribeContext>().Terminate());
            chains.AddChain(cb => cb.Begin<ForwardSendContext>().Terminate());
            chains.AddChain(cb => cb.Begin<ForwardPublishContext>().Terminate());
            chains.AddChain(cb => cb.Begin<ForwardReplyContext>().Terminate());

            chains.AddChain(cb => cb.Begin<AnycastContext>().AddSection<PostroutingContext>().Terminate());
            chains.AddChain(cb => cb.Begin<MulticastContext>().AddSection<PostroutingContext>().Terminate());
            chains.AddChain(cb => cb.Begin<PostroutingContext>().Terminate());

            chains.AddRule(_ => new RawToPreroutingConnector());
            chains.AddRule(_ => new PreroutingToSubscribePreroutingFork());
            chains.AddRule(_ => new PreroutingToSendPreroutingFork());
            chains.AddRule(_ => new PreroutingToPublishPreroutingFork());
            chains.AddRule(_ => new PreroutingToReplyPreroutingFork());
            chains.AddRule(_ => new PreroutingTerminator());

            chains.AddRule(c => new DetectCyclesRule(c.Endpoint.EndpointName));

            chains.AddRule(c => new SubscribePreroutingTerminator(config.RoutingProtocol, c.TypeGenerator));
            chains.AddRule(c => new UnsubscribePreroutingTerminator(config.RoutingProtocol, c.TypeGenerator));
            chains.AddRule(_ => new SendPreroutingTerminator(config.RoutingProtocol));
            chains.AddRule(c => new PublishPreroutingTerminator(interfaces.Select(i => i.Name).ToArray(), c.TypeGenerator));
            chains.AddRule(_ => new ReplyPreroutingTerminator());

            chains.AddRule(_ => new FindSubscribeDestinationsByHeadersRule());
            chains.AddRule(_ => new FindUnsubscribeDestinationsByHeadersRule());
            chains.AddRule(_ => new FindSendDestinationsByHeadersRule());


            chains.AddRule(c => new ForwardPublishNativeRule(), c => c.HasNativePubSub());
            chains.AddRule(c => new ForwardSubscribeNativeRule(c.Endpoint.SubscriptionManager), c => c.HasNativePubSub());
            chains.AddRule(c => new ForwardUnsubscribeNativeRule(c.Endpoint.SubscriptionManager), c => c.HasNativePubSub());

            chains.AddRule(c => new ForwardPublishStorageDrivenRule(c.SubscriptionPersistence, c.DistributionPolicy), c => !c.HasNativePubSub());
            chains.AddRule(c => new ForwardSubscribeMessageDrivenRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName), c => !c.HasNativePubSub());
            chains.AddRule(c => new ForwardUnsubscribeMessageDrivenRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName), c => !c.HasNativePubSub());
            chains.AddRule(c => new StorageDrivenSubscriptionRule(c.SubscriptionPersistence), c => !c.HasNativePubSub());

            chains.AddRule(c => new ForwardSendRule(c.Endpoint.TransportAddress));
            chains.AddRule(c => new ForwardSendGatewayRule(c.Endpoint.EndpointName));
            chains.AddRule(_ => new ForwardReplyRule());
            chains.AddRule(c => new ForwardSubscribeGatewayRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));
            chains.AddRule(c => new ForwardUnsubscribeGatewayRule(c.Endpoint.TransportAddress, c.Endpoint.EndpointName));

            chains.AddRule(c => new AnycastToPostroutingConnector(c.EndpointInstances, c.DistributionPolicy, instance => c.Endpoint.ToTransportAddress(LogicalAddress.CreateRemoteAddress(instance))));
            chains.AddRule(c => new MulticastToPostroutingConnector(c.EndpointInstances, instance => c.Endpoint.ToTransportAddress(LogicalAddress.CreateRemoteAddress(instance))));
            chains.AddRule(c => new PostroutingTerminator(c.Endpoint));

            chains.AddRule(_ => new ForwardSubscribeTerminator());
            chains.AddRule(_ => new ForwardUnsubscribeTerminator());
            chains.AddRule(_ => new ForwardSendTerminator());
            chains.AddRule(_ => new ForwardPublishTerminator());
            chains.AddRule(_ => new ForwardReplyTerminator());

            return new RouterImpl(config.Name, interfaces, config.Modules.ToArray(), config.RoutingProtocol, chains);
        }
    }
}