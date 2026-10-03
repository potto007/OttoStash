using OttoStash.Stands;
using ItemType = ItemDrop.ItemData.ItemType;

namespace OttoStash.Tests;

public class StandRulesTests
{
    [Fact]
    public void A_slot_with_no_types_takes_anything()
    {
        Assert.True(StandRules.TypeFits(Array.Empty<ItemType>(), ItemType.Trophy, ItemType.None));
    }

    [Fact]
    public void A_typed_slot_takes_only_its_types()
    {
        ItemType[] head = { ItemType.Helmet };

        Assert.True(StandRules.TypeFits(head, ItemType.Helmet, ItemType.None));
        Assert.False(StandRules.TypeFits(head, ItemType.Chest, ItemType.None));
    }

    [Fact]
    public void The_attach_override_wins_over_the_item_type()
    {
        ItemType[] hand = { ItemType.OneHandedWeapon };

        Assert.True(StandRules.TypeFits(hand, ItemType.Tool, ItemType.OneHandedWeapon));
        Assert.False(StandRules.TypeFits(hand, ItemType.OneHandedWeapon, ItemType.Shield));
    }

    [Theory]
    [InlineData(ItemType.Chest, false)]
    [InlineData(ItemType.Legs, false)]
    [InlineData(ItemType.Helmet, true)]
    [InlineData(ItemType.Shield, true)]
    public void Only_body_pieces_skip_the_attach_point(ItemType type, bool needsAttachPoint)
    {
        Assert.Equal(needsAttachPoint, StandRules.NeedsAttachPoint(type));
    }

    [Fact]
    public void Labels_read_as_words()
    {
        Assert.Equal("Any item", StandRules.Label(Array.Empty<ItemType>()));
        Assert.Equal("Helmet", StandRules.Label(new[] { ItemType.Helmet }));
        Assert.Equal("One handed weapon / Shield", StandRules.Label(new[] { ItemType.OneHandedWeapon, ItemType.Shield }));
        Assert.Equal("Attach atgeir", StandRules.Label(new[] { ItemType.Attach_Atgeir }));
    }
}
