using OttoStash.Storing;

namespace OttoStash.Tests;

public class ChestGateTests
{
    [Theory]
    // owner flag set, synced flag set, or both: someone else has it open
    [InlineData(false, true, 0, false, true)]
    [InlineData(false, false, 1, false, true)]
    [InlineData(false, true, 1, false, true)]
    // neither flag: free
    [InlineData(false, false, 0, false, false)]
    // the chest in the local player's own inventory screen is theirs
    [InlineData(true, true, 1, false, false)]
    // MultiUserChest arbitrates access itself
    [InlineData(false, true, 1, true, false)]
    public void Open_elsewhere_rule(bool isLocalPlayersOpenChest, bool ownerInUseFlag, int syncedInUse, bool multiUserChest, bool expected)
    {
        Assert.Equal(expected, ChestGate.IsOpenElsewhere(isLocalPlayersOpenChest, ownerInUseFlag, syncedInUse, multiUserChest));
    }

    [Fact]
    public void A_missing_chest_is_not_open()
    {
        Assert.False(ChestGate.IsOpenElsewhere((Container?)null));
    }
}
