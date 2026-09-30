using OttoStash.Interfaces;
using OttoStash.Storing;

namespace OttoStash.Tests;

public class StoreRunTests
{
    private static readonly Action<string> Quiet = _ => { };

    private static int Run(FakeChestAccess chests, bool multiUserChest, params FakeTarget[] targets)
    {
        return StoreRun.Run(targets, t => t.TryStore(), chests, multiUserChest, Quiet, Quiet);
    }

    [Fact]
    public void Sums_every_target_into_one_total()
    {
        FakeChestAccess chests = new();
        FakeTarget a = new("a", isChest: true, store: () => 3);
        FakeTarget b = new("b", isChest: true, store: () => 4);
        FakeTarget drawer = new("drawer", isChest: false, store: () => 5);

        int total = Run(chests, false, a, b, drawer);

        Assert.Equal(12, total);
        Assert.Equal(1, a.StoreCalls);
        Assert.Equal(1, b.StoreCalls);
        Assert.Equal(1, drawer.StoreCalls);
    }

    [Fact]
    public void Skips_a_chest_another_player_has_open_before_any_ownership_claim()
    {
        FakeChestAccess chests = new();
        FakeTarget open = new("open", isChest: true, owned: false, store: () => 9) { OpenElsewhere = true };
        FakeTarget free = new("free", isChest: true, owned: false, store: () => 2);

        int total = Run(chests, false, open, free);

        Assert.Equal(2, total);
        Assert.Equal(0, open.StoreCalls);
        Assert.DoesNotContain("claim open", chests.Ledger);
        Assert.DoesNotContain("hold open", chests.Ledger);
        Assert.Equal(new[] { "gate open", "gate free", "claim free", "hold free", "release free" }, chests.Ledger);
    }

    [Fact]
    public void Releases_the_chest_when_the_store_throws_and_carries_on()
    {
        FakeChestAccess chests = new();
        FakeTarget broken = new("broken", isChest: true, store: () => throw new InvalidOperationException("boom"));
        FakeTarget next = new("next", isChest: true, store: () => 1);

        int total = Run(chests, false, broken, next);

        Assert.Equal(1, total);
        Assert.Empty(chests.Held);
        Assert.Contains("release broken", chests.Ledger);
        Assert.Equal(1, next.StoreCalls);
    }

    [Fact]
    public void Skips_a_chest_it_cannot_take_ownership_of()
    {
        FakeChestAccess chests = new();
        FakeTarget stubborn = new("stubborn", isChest: true, owned: false, store: () => 7) { OwnershipClaimSucceeds = false };

        int total = Run(chests, false, stubborn);

        Assert.Equal(0, total);
        Assert.Equal(0, stubborn.StoreCalls);
        Assert.DoesNotContain("hold stubborn", chests.Ledger);
    }

    [Fact]
    public void Claims_ownership_of_an_unowned_free_chest_and_holds_it_for_the_store()
    {
        FakeChestAccess chests = new();
        FakeTarget chest = new("chest", isChest: true, owned: false, store: () => 1);

        Run(chests, false, chest);

        Assert.True(chest.Owned);
        Assert.Equal(new[] { "gate chest", "claim chest", "hold chest", "release chest" }, chests.Ledger);
    }

    [Fact]
    public void With_MultiUserChest_active_stores_without_gate_claim_or_hold()
    {
        FakeChestAccess chests = new();
        FakeTarget chest = new("chest", isChest: true, owned: false, store: () => 6) { OpenElsewhere = true };

        int total = Run(chests, true, chest);

        Assert.Equal(6, total);
        Assert.Empty(chests.Ledger);
    }

    [Fact]
    public void Touches_a_drawer_or_backpack_only_when_this_client_owns_it()
    {
        FakeChestAccess chests = new();
        FakeTarget owned = new("owned", isChest: false, owned: true, store: () => 2);
        FakeTarget foreign = new("foreign", isChest: false, owned: false, store: () => 2);

        int total = Run(chests, false, owned, foreign);

        Assert.Equal(2, total);
        Assert.Equal(0, foreign.StoreCalls);
        Assert.Empty(chests.Ledger);
    }

    [Fact]
    public void Reports_zero_and_touches_nothing_with_no_targets()
    {
        FakeChestAccess chests = new();

        int total = StoreRun.Run(Array.Empty<IContainer>(), t => t.TryStore(), chests, false, Quiet, Quiet);

        Assert.Equal(0, total);
        Assert.Empty(chests.Ledger);
    }
}
