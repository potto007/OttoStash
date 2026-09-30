using OttoStash.Util;

namespace OttoStash.Tests;

/// Runs the chunked move against the game's real Inventory class.
public class InventoryMoveTests
{
    [Fact]
    public void Moves_a_whole_stack_into_an_empty_slot()
    {
        Inventory chest = Items.Chest(2, 1);
        ItemDrop.ItemData wood = Items.Stack("$item_wood", 30);

        int moved = InventoryMove.MoveStackChunked(chest, wood);

        Assert.Equal(30, moved);
        Assert.Equal(0, wood.m_stack);
        Assert.Equal(30, chest.CountItems("$item_wood"));
    }

    [Fact]
    public void Tops_up_a_partial_stack_when_less_than_half_the_incoming_stack_fits()
    {
        Inventory chest = Items.Chest(1, 1);
        chest.AddItem(Items.Stack("$item_wood", 40));
        ItemDrop.ItemData wood = Items.Stack("$item_wood", 30);

        int moved = InventoryMove.MoveStackChunked(chest, wood);

        Assert.Equal(10, moved);
        Assert.Equal(20, wood.m_stack);
        Assert.Equal(50, chest.CountItems("$item_wood"));
    }

    [Fact]
    public void Fills_a_partial_stack_and_an_empty_slot_in_one_move()
    {
        Inventory chest = Items.Chest(2, 1);
        chest.AddItem(Items.Stack("$item_wood", 45));
        ItemDrop.ItemData wood = Items.Stack("$item_wood", 30);

        int moved = InventoryMove.MoveStackChunked(chest, wood);

        Assert.Equal(30, moved);
        Assert.Equal(0, wood.m_stack);
        Assert.Equal(75, chest.CountItems("$item_wood"));
    }

    [Fact]
    public void Moves_what_fits_and_leaves_the_rest_when_the_chest_is_nearly_full()
    {
        Inventory chest = Items.Chest(1, 1);
        chest.AddItem(Items.Stack("$item_wood", 49));
        ItemDrop.ItemData wood = Items.Stack("$item_wood", 30);

        int moved = InventoryMove.MoveStackChunked(chest, wood);

        Assert.Equal(1, moved);
        Assert.Equal(29, wood.m_stack);
    }

    [Fact]
    public void Moves_nothing_into_a_full_chest()
    {
        Inventory chest = Items.Chest(1, 1);
        chest.AddItem(Items.Stack("$item_wood", 50));
        ItemDrop.ItemData wood = Items.Stack("$item_wood", 30);

        int moved = InventoryMove.MoveStackChunked(chest, wood);

        Assert.Equal(0, moved);
        Assert.Equal(30, wood.m_stack);
    }

    [Fact]
    public void Moves_nothing_for_an_empty_source_stack()
    {
        Inventory chest = Items.Chest(2, 2);

        Assert.Equal(0, InventoryMove.MoveStackChunked(chest, Items.Stack("$item_wood", 0)));
    }

    [Fact]
    public void Does_not_merge_into_a_different_item()
    {
        Inventory chest = Items.Chest(1, 1);
        chest.AddItem(Items.Stack("$item_stone", 10));
        ItemDrop.ItemData wood = Items.Stack("$item_wood", 30);

        int moved = InventoryMove.MoveStackChunked(chest, wood);

        Assert.Equal(0, moved);
        Assert.Equal(0, chest.CountItems("$item_wood"));
    }
}
