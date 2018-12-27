using System.Data.SqlClient;
using System.Threading.Tasks;
using NServiceBus.Logging;
using NServiceBus.Router;
using NServiceBus.Router.Deduplication.Inbox;
using NUnit.Framework;

[TestFixture]
public class InboxPersisterTests
{
    InboxPersister persister;
    InboxInstaller installer;

    [SetUp]
    public async Task PrepareTables()
    {
        LogManager.Use<DefaultFactory>().Level(LogLevel.Debug);

        installer = new InboxInstaller("D");
        persister = new InboxPersister("S", "D", CreateConnection);
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            using (var trans = conn.BeginTransaction())
            {
                await installer.Uninstall("S", conn, trans).ConfigureAwait(false);
                await installer.Install("S", conn, trans).ConfigureAwait(false);
                trans.Commit();
            }

            await persister.Prepare().ConfigureAwait(false);
        }
    }

    static SqlConnection CreateConnection()
    {
        return new SqlConnection("data source = (local); initial catalog=test1; integrated security=true");
    }

    [Test]
    public async Task It_does_not_advance_if_state_has_not_been_initialized()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            try
            {
                await persister.Advance(2, 6, 9, conn).ConfigureAwait(false);
                Assert.Fail("Expected exception");
            }
            catch (ProcessCurrentMessageLaterException e)
            {
                Assert.AreEqual("Link state for S is not yet initialized. Cannot advance the epoch.", e.Message);
            }
        }
    }

    [Test]
    public async Task It_does_not_advance_if_table_has_holes()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            try
            {
                await persister.Advance(2, 6, 9, conn).ConfigureAwait(false);
                Assert.Fail("Expected exception");
            }
            catch (ProcessCurrentMessageLaterException e)
            {
                StringAssert.StartsWith("Inbox table Inbox_S_D_Right seems to have holes in the sequence.", e.Message);
            }
        }
    }

    [Test]
    public async Task It_does_not_advance_if_new_epoch_is_higher_then_expected()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M2", 1, conn, null).ConfigureAwait(false);

            try
            {
                await persister.Advance(3, 6, 9, conn).ConfigureAwait(false);
                Assert.Fail("Expected exception");
            }
            catch (ProcessCurrentMessageLaterException e)
            {
                Assert.AreEqual("The link state is at epoch 1 and is not ready to transition to epoch 3.", e.Message);
            }
        }
    }

    [Test]
    public async Task It_ignores_old_advance_epoch_messages()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M2", 1, conn, null).ConfigureAwait(false);

            var linkState = await persister.Advance(1, 3, 6, conn).ConfigureAwait(false);

            Assert.AreEqual(6, linkState.HeadSession.Hi);
            Assert.AreEqual(0, linkState.TailSession.Lo);
        }
    }

    [Test]
    public async Task It_does_advance_if_table_does_not_have_holes()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M2", 1, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M3", 2, conn, null).ConfigureAwait(false);

            var linkState = await persister.Advance(2, 6, 9, conn).ConfigureAwait(false);

            Assert.AreEqual(9, linkState.HeadSession.Hi);
            Assert.AreEqual(3, linkState.TailSession.Lo);
        }
    }

    [Test]
    public async Task It_fails_when_trying_to_deduplicate()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M2", 1, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M3", 2, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M4", 3, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M5", 4, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M6", 5, conn, null).ConfigureAwait(false);

            try
            {
                await persister.Deduplicate("M7", 6, conn, null).ConfigureAwait(false);
            }
            catch (ProcessCurrentMessageLaterException e)
            {
                Assert.AreEqual("The message requires advancing epoch. Moving it to the back of the queue.", e.Message);
            }
        }
    }

    [Test]
    public async Task It_de_duplicates_messages()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M2", 1, conn, null).ConfigureAwait(false);
            var result = await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);

            Assert.AreEqual(DeduplicationResult.Duplicate, result);
        }
    }

    [Test]
    public async Task It_refreshes_state_if_stale_state_is_detected()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            var slowPersister = new InboxPersister("S", "D", CreateConnection);
            await slowPersister.Prepare();

            await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M2", 1, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M3", 2, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M4", 3, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M5", 4, conn, null).ConfigureAwait(false);

            await persister.Advance(2, 6, 9, conn);

            await slowPersister.Deduplicate("M7", 6, conn, null);
        }
    }

    [Test]
    public async Task It_refreshes_state_when_constraint_violation_is_detected()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            var slowPersister = new InboxPersister("S", "D", CreateConnection);
            await slowPersister.Prepare();

            await persister.Deduplicate("M1", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M2", 1, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M3", 2, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M4", 3, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M5", 4, conn, null).ConfigureAwait(false);

            await persister.Advance(2, 6, 9, conn);

            var result = await slowPersister.Deduplicate("M2", 1, conn, null);

            Assert.AreEqual(DeduplicationResult.Duplicate, result);
        }
    }

    [Test]
    public async Task It_uses_refreshed_state_to_detect_duplicates()
    {
        using (var conn = CreateConnection())
        {
            await conn.OpenAsync().ConfigureAwait(false);

            await persister.Initialize(3, 6, 0, 3, conn).ConfigureAwait(false);

            var slowPersister = new InboxPersister("S", "D", CreateConnection);
            await slowPersister.Prepare();

            await persister.Deduplicate("M0", 0, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M0", 1, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M0", 2, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M0", 3, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M0", 4, conn, null).ConfigureAwait(false);

            await persister.Advance(2, 6, 9, conn);

            await persister.Deduplicate("M5", 5, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M6", 6, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M7", 7, conn, null).ConfigureAwait(false);

            await persister.Advance(3, 9, 12, conn);

            await persister.Deduplicate("M8", 8, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M9", 9, conn, null).ConfigureAwait(false);
            await persister.Deduplicate("M10", 10, conn, null).ConfigureAwait(false);

            await persister.Advance(4, 12, 15, conn);

            var result = await slowPersister.Deduplicate("M6", 6, conn, null);

            Assert.AreEqual(DeduplicationResult.Duplicate, result);
        }
    }
}