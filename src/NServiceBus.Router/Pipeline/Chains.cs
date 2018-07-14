using System;
using System.Collections.Generic;
using NServiceBus.Router;

class Chains : IChains
{
    Dictionary<Type, object> chains = new Dictionary<Type, object>();

    public Chains(ChainBuilder builder)
    {
        chains[typeof(RawContext)] = builder.Begin<RawContext>().AddSection<PreroutingContext>().Terminate();
        chains[typeof(SubscribePreroutingContext)] = builder.Begin<SubscribePreroutingContext>().Terminate();
        chains[typeof(UnsubscribePreroutingContext)] = builder.Begin<UnsubscribePreroutingContext>().Terminate();
        chains[typeof(SendPreroutingContext)] = builder.Begin<SendPreroutingContext>().Terminate();
        chains[typeof(PublishPreroutingContext)] = builder.Begin<PublishPreroutingContext>().Terminate();
        chains[typeof(ReplyPreroutingContext)] = builder.Begin<ReplyPreroutingContext>().Terminate();

        chains[typeof(ForwardSubscribeContext)] = builder.Begin<ForwardSubscribeContext>().Terminate();
        chains[typeof(ForwardUnsubscribeContext)] = builder.Begin<ForwardUnsubscribeContext>().Terminate();
        chains[typeof(ForwardSendContext)] = builder.Begin<ForwardSendContext>().Terminate();
        chains[typeof(ForwardPublishContext)] = builder.Begin<ForwardPublishContext>().Terminate();
        chains[typeof(ForwardReplyContext)] = builder.Begin<ForwardReplyContext>().Terminate();

        chains[typeof(AnycastContext)] = builder.Begin<AnycastContext>().AddSection<PostroutingContext>().Terminate();
        chains[typeof(MulticastContext)] = builder.Begin<MulticastContext>().AddSection<PostroutingContext>().Terminate();
        chains[typeof(PostroutingContext)] = builder.Begin<PostroutingContext>().Terminate();
    }

    public IChain<T> Get<T>() where T : IRuleContext
    {
        return (IChain<T>)chains[typeof(T)];
    }
}