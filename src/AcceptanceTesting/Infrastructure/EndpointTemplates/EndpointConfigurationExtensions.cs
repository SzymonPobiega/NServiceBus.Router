namespace NServiceBus.AcceptanceTests
{
    using System;
    using AcceptanceTesting.Support;
    using Configuration.AdvancedExtensibility;

    public static class EndpointConfigurationExtensions
    {
        public static void ConfigureTransport<T>(this EndpointConfiguration endpointConfiguration, Action<TransportExtensions> configureTransport = null)
            where T : IConfigureEndpointTestExecution, new()
        {
            var config = new T
            {
                ConfigureTransport = configureTransport
            };
            endpointConfiguration.GetSettings().Set<IConfigureEndpointTestExecution>(config);
        }
    }
}