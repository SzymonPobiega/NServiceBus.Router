using NServiceBus.Router.Deduplication.Outbox;
using NUnit.Framework;

[TestFixture]
class LinkStateTests
{
    [Test]
    public void Uninitialized_state_contains_no_head_nor_tails()
    {
        var state = LinkState.Uninitialized();

        Assert.IsNull(state.HeadSession);
        Assert.IsNull(state.TailSession);
    }

    [Test]
    public void Epoch_number_is_1_for_freshly_initialized_link_state()
    {
        var state = LinkState.Uninitialized();
        state = state.Initialize("Head", "Tail", 10);

        Assert.AreEqual(1, state.Epoch);
    }

    [Test]
    public void Head_table_range_is_initialized()
    {
        var state = LinkState.Uninitialized();
        state = state.Initialize("Head", "Tail", 10);

        Assert.AreEqual(10, state.HeadSession.Lo);
        Assert.AreEqual(20, state.HeadSession.Hi);
        Assert.AreEqual("Head", state.HeadSession.Table);
    }

    [Test]
    public void Tail_table_range_is_initialized()
    {
        var state = LinkState.Uninitialized();
        state = state.Initialize("Head", "Tail", 10);

        Assert.AreEqual(0, state.TailSession.Lo);
        Assert.AreEqual(10, state.TailSession.Hi);
        Assert.AreEqual("Tail", state.TailSession.Table);
    }

    [Test]
    public void Advancing_epoch_switches_head_and_tail_tables()
    {
        var state = LinkState.Uninitialized();
        state = state.Initialize("Head", "Tail", 10);

        state = state.Advance(15);

        Assert.AreEqual(20, state.HeadSession.Lo);
        Assert.AreEqual(35, state.HeadSession.Hi);
        Assert.AreEqual("Tail", state.HeadSession.Table);

        Assert.AreEqual(10, state.TailSession.Lo);
        Assert.AreEqual(20, state.TailSession.Hi);
        Assert.AreEqual("Head", state.TailSession.Table);
    }
}
