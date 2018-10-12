using System.Linq;
using System.Threading.Tasks;
using NServiceBus.Router;

class PublishPreroutingTerminator : ChainTerminator<PublishPreroutingContext>
{
    string[] allInterfaces;
    RuntimeTypeGenerator typeGenerator;

    public PublishPreroutingTerminator(string[] allInterfaces, RuntimeTypeGenerator typeGenerator)
    {
        this.allInterfaces = allInterfaces;
        this.typeGenerator = typeGenerator;
    }

    protected override Task Terminate(PublishPreroutingContext context)
    {
        var outgoingInterfaces = allInterfaces.Where(i => i != context.IncomingInterface);

        var interfaces = context.Extensions.Get<IInterfaceChains>();
        var forkTasks = outgoingInterfaces
            .Select(iface =>
            {
                var chains = interfaces.GetChainsFor(iface);
                var chain = chains.Get<ForwardPublishContext>();
                return chain.Invoke(new ForwardPublishContext(iface, typeGenerator.GetType(context.Types.First()), context));
            });

        return Task.WhenAll(forkTasks);
    }
}