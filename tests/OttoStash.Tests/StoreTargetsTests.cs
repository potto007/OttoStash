using OttoStash.Storing;

namespace OttoStash.Tests;

public class StoreTargetsTests
{
    [Fact]
    public void A_live_player_built_chest_with_an_inventory_inside_the_ward_is_eligible()
    {
        Assert.True(StoreTargets.IsEligible(liveNetworkObject: true, playerBuilt: true, hasInventory: true, carriedByAnotherCharacter: false, wardAccess: true));
    }

    [Theory]
    [InlineData(false, true, true, false, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, true, true, true, true)]
    [InlineData(true, true, true, false, false)]
    public void Any_failed_rule_makes_a_container_ineligible(bool live, bool playerBuilt, bool hasInventory, bool carriedByAnother, bool wardAccess)
    {
        Assert.False(StoreTargets.IsEligible(live, playerBuilt, hasInventory, carriedByAnother, wardAccess));
    }

    [Fact]
    public void A_null_container_is_ineligible()
    {
        Assert.False(StoreTargets.IsEligible((Container)null!));
    }
}
