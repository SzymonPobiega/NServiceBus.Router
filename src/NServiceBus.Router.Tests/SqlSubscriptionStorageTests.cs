using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Linq;
using NServiceBus.Extensibility;
using NServiceBus.Logging;
using NServiceBus.Router;
using NServiceBus.Unicast.Subscriptions;
using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
using NUnit.Framework;

[TestFixture]
public class SqlSubscriptionStorageTests
{
    SqlSubscriptionStorage subscriptionStorage;

    [SetUp]
    public async Task PrepareTables()
    {
        LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

        subscriptionStorage = new SqlSubscriptionStorage(CreateConnection, "", new SqlDialect.MsSqlServer(), null);

        await subscriptionStorage.Uninstall();
        await subscriptionStorage.Install();
    }

    [Test]
    public async Task Can_subscribe_with_both_transport_address_and_endpoint_name()
    {
        var messageType = new MessageType(typeof(SqlSubscriptionStorageTests));
        await subscriptionStorage.Subscribe(new Subscriber("address", "endpoint"), messageType, new ContextBag());

        var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] {messageType}, new ContextBag());

        var singleSubscriber = subscribers.Single();
        Assert.AreEqual("address", singleSubscriber.TransportAddress);
        Assert.AreEqual("endpoint", singleSubscriber.Endpoint);
    }

    [Test]
    public async Task Can_unsubscribe_with_both_transport_address_and_endpoint_name()
    {
        var messageType = new MessageType(typeof(SqlSubscriptionStorageTests));
        await subscriptionStorage.Subscribe(new Subscriber("address", "endpoint"), messageType, new ContextBag());

        var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, new ContextBag());

        var singleSubscriber = subscribers.Single();
        Assert.AreEqual("address", singleSubscriber.TransportAddress);
        Assert.AreEqual("endpoint", singleSubscriber.Endpoint);

        await subscriptionStorage.Unsubscribe(new Subscriber("address", "endpoint"), messageType, new ContextBag());
        subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, new ContextBag());

        CollectionAssert.IsEmpty(subscribers);
    }

    [Test]
    public async Task Can_subscribe_with_address_only()
    {
        var messageType = new MessageType(typeof(SqlSubscriptionStorageTests));
        await subscriptionStorage.Subscribe(new Subscriber("address", null), messageType, new ContextBag());

        var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, new ContextBag());

        var singleSubscriber = subscribers.Single();
        Assert.IsNull(singleSubscriber.Endpoint);
        Assert.AreEqual("address", singleSubscriber.TransportAddress);
    }

    [Test]
    public async Task Can_unsubscribe_with_address_only()
    {
        var messageType = new MessageType(typeof(SqlSubscriptionStorageTests));
        await subscriptionStorage.Subscribe(new Subscriber("address", null), messageType, new ContextBag());

        var subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, new ContextBag());

        var singleSubscriber = subscribers.Single();
        Assert.IsNull(singleSubscriber.Endpoint);
        Assert.AreEqual("address", singleSubscriber.TransportAddress);

        await subscriptionStorage.Unsubscribe(new Subscriber("address", null), messageType, new ContextBag());
        subscribers = await subscriptionStorage.GetSubscriberAddressesForMessage(new[] { messageType }, new ContextBag());

        CollectionAssert.IsEmpty(subscribers);
    }

    static SqlConnection CreateConnection()
    {
        return new SqlConnection("data source = (local); initial catalog=test1; integrated security=true");
    }
}
