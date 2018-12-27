using NServiceBus.Router;
using NServiceBus.Raw;
using NServiceBus.Routing;
using NServiceBus.Settings;

class RuleCreationContext : IRuleCreationContext
{
    public string InterfaceName { get; }
    public EndpointInstances EndpointInstances { get; }
    public RawDistributionPolicy DistributionPolicy { get; }
    public IRawEndpoint Endpoint { get; }
    public RuntimeTypeGenerator TypeGenerator { get; }
    public ReadOnlySettings Settings { get; }

    public RuleCreationContext(string interfaceName, EndpointInstances endpointInstances, RawDistributionPolicy distributionPolicy, IRawEndpoint endpoint, RuntimeTypeGenerator typeGenerator, ReadOnlySettings settings)
    {
        InterfaceName = interfaceName;
        EndpointInstances = endpointInstances;
        DistributionPolicy = distributionPolicy;
        Endpoint = endpoint;
        TypeGenerator = typeGenerator;
        Settings = settings;
    }
}