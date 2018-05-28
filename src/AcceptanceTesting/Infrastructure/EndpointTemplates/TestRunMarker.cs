namespace NServiceBus.AcceptanceTests.EndpointTemplates
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;

    class TestRunMarker : Behavior<IDispatchContext>
    {
        string testRunId;

        public TestRunMarker(string testRunId)
        {
            this.testRunId = testRunId;
        }

        public override Task Invoke(IDispatchContext context, Func<Task> next)
        {
            foreach (var operation in context.Operations)
            {
                operation.Message.Headers["NServiceBus.Router.TestRunId"] = testRunId;
            }

            return next();
        }
    }
}