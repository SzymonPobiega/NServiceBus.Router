namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting.Customization;
    using AcceptanceTesting.Support;

    public class DefaultServer : IEndpointSetupTemplate
    {
        public IConfigureEndpointTestExecution TransportConfiguration { get; set; } = TestSuiteConstraints.Current.CreateTransportConfiguration();

        public async Task<EndpointConfiguration> GetConfiguration(RunDescriptor runDescriptor, EndpointCustomizationConfiguration endpointConfiguration, Func<EndpointConfiguration, Task> configurationBuilderCustomization)
        {
            var builder = new EndpointConfiguration(endpointConfiguration.EndpointName);
            builder.EnableInstallers();

            builder.Recoverability()
                .Delayed(delayed => delayed.NumberOfRetries(0))
                .Immediate(immediate => immediate.NumberOfRetries(0));
            builder.SendFailedMessagesTo("error");
            builder.UseSerialization<NewtonsoftJsonSerializer>();

            await builder.DefineTransport(TransportConfiguration, runDescriptor, endpointConfiguration).ConfigureAwait(false);

            await configurationBuilderCustomization(builder).ConfigureAwait(false);

            builder.Pipeline.Register(new TestRunMarker(runDescriptor.ScenarioContext.TestRunId.ToString()), "Marks messages with test run ID.");

            // scan types at the end so that all types used by the configuration have been loaded into the AppDomain
            builder.ScanTypesForTest(endpointConfiguration);

            return builder;
        }
    }
}