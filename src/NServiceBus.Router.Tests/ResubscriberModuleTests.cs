using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Router.Resubscriber;
using NUnit.Framework;

[TestFixture]
public class ResubscriberModuleTests
{
    [Test]
    public async Task It_deduplicates_entries_based_on_subscriber_and_type_by_discarding_older_messages()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type1", MessageIntentEnum.Subscribe, "B", new DateTime(2018, 09, 14, 12, 55, 0)));

        Assert.AreEqual(1, output.Count);
    }

    [Test]
    public async Task It_does_not_deduplicate_entries_messages_with_same_id()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 55, 0)));

        Assert.AreEqual(2, output.Count);
    }

    [Test]
    public async Task It_does_not_deduplicate_entries_messages_with_different_type()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type2", MessageIntentEnum.Subscribe, "B", new DateTime(2018, 09, 14, 12, 55, 0)));

        Assert.AreEqual(2, output.Count);
    }


    [Test]
    public async Task It_does_not_discard_newer_messages()
    {
        var output = await RunResubscriberOnce(
            CreateMessageContext("type1", MessageIntentEnum.Subscribe, "A", new DateTime(2018, 09, 14, 12, 56, 0)),
            CreateMessageContext("type1", MessageIntentEnum.Subscribe, "B", new DateTime(2018, 09, 14, 12, 57, 0)));

        Assert.AreEqual(2, output.Count);
    }

    static async Task<List<Result>> RunResubscriberOnce(params Operation[] input)
    {
        await Cleanup("ResubscriberTest", "ResubscriberTest.Resubscriber", "poison").ConfigureAwait(false);

        var resubscriber = new StorageModule<MsmqTransport>("ResubscriberTest", TimeSpan.FromSeconds(3), t => { });
        var subscribeInterceptor = new SubscribeInterceptor();
        var chains = new InterfaceChains();
        var ruleCreationContext = new RuleCreationContext("ResubscriberTest", null, null, null, null, null);

        chains.AddChain(cb => cb.Begin<SubscribeContext>().Terminate());
        chains.AddRule(c => subscribeInterceptor);
        chains.InitializeInterface("ResubscriberTest", ruleCreationContext);

        await resubscriber.Start(new RootContext(chains));

        foreach (var op in input)
        {
            await resubscriber.Enqueue(op.Type, op.Intent.ToString(), op.Timestamp, op.Id).ConfigureAwait(false);
        }
        await resubscriber.Enqueue("stop", "Subscribe", DateTime.UtcNow, Guid.NewGuid().ToString());

        await subscribeInterceptor.Done.Task;

        await resubscriber.Stop();

        return subscribeInterceptor.Invocations;
    }

    static Operation CreateMessageContext(string messageType, MessageIntentEnum intent, string id, DateTime timestamp)
    {
        return new Operation(messageType, intent, id, timestamp);
    }

    static Task Cleanup(params string[] queueNames)
    {
        var allQueues = MessageQueue.GetPrivateQueuesByMachine("localhost");
        var queuesToBeDeleted = new List<string>();

        foreach (var messageQueue in allQueues)
        {
            using (messageQueue)
            {
                if (queueNames.Any(ra =>
                {
                    var indexOfAt = ra.IndexOf("@", StringComparison.Ordinal);
                    if (indexOfAt >= 0)
                    {
                        ra = ra.Substring(0, indexOfAt);
                    }
                    return messageQueue.QueueName.StartsWith(@"private$\" + ra, StringComparison.OrdinalIgnoreCase);
                }))
                {
                    queuesToBeDeleted.Add(messageQueue.Path);
                }
            }
        }

        foreach (var queuePath in queuesToBeDeleted)
        {
            try
            {
                MessageQueue.Delete(queuePath);
                Console.WriteLine("Deleted '{0}' queue", queuePath);
            }
            catch (Exception)
            {
                Console.WriteLine("Could not delete queue '{0}'", queuePath);
            }
        }

        MessageQueue.ClearConnectionCache();

        return Task.FromResult(0);
    }

    class SubscribeInterceptor : ChainTerminator<SubscribeContext>
    {
        public TaskCompletionSource<bool> Done = new TaskCompletionSource<bool>();

        public List<Result> Invocations = new List<Result>();

        protected override Task Terminate(SubscribeContext context)
        {
            if (context.MessageType == "stop")
            {
                Done.SetResult(true);
            }
            else
            {
                Invocations.Add(new Result(context.MessageType, MessageIntentEnum.Subscribe));
            }
            return Task.CompletedTask;
        }
    }

    class Result
    {
        public Result(string type, MessageIntentEnum intent)
        {
            Type = type;
            Intent = intent;
        }

        public string Type { get; }
        public MessageIntentEnum Intent { get; }
    }

    class Operation
    {
        public Operation(string type, MessageIntentEnum intent, string id, DateTime timestamp)
        {
            Type = type;
            Intent = intent;
            Timestamp = timestamp;
            Id = id;
        }

        public string Type { get; }
        public MessageIntentEnum Intent { get; }
        public DateTime Timestamp { get; }
        public string Id { get; }
    }

}
