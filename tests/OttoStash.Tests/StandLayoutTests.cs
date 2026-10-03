using OttoStash.Stands;

namespace OttoStash.Tests;

public class StandLayoutTests
{
    [Fact]
    public void A_full_stand_is_drawn_as_a_figure()
    {
        VisSlot[] slots = { VisSlot.HandRight, VisSlot.HandLeft, VisSlot.Helmet, VisSlot.Chest, VisSlot.Legs, VisSlot.Shoulder, VisSlot.Utility };

        Vector2i[] cells = StandLayout.Place(slots, out int height);

        Assert.Equal(3, height);
        Assert.Equal(new Vector2i(2, 1), cells[0]);
        Assert.Equal(new Vector2i(0, 1), cells[1]);
        Assert.Equal(new Vector2i(1, 0), cells[2]);
        Assert.Equal(new Vector2i(1, 1), cells[3]);
        Assert.Equal(new Vector2i(1, 2), cells[4]);
        Assert.Equal(new Vector2i(0, 2), cells[5]);
        Assert.Equal(new Vector2i(2, 2), cells[6]);
    }

    [Fact]
    public void A_repeated_slot_takes_the_first_free_cell()
    {
        VisSlot[] slots = { VisSlot.HandRight, VisSlot.HandRight, VisSlot.Helmet };

        Vector2i[] cells = StandLayout.Place(slots, out int height);

        Assert.Equal(new Vector2i(2, 1), cells[0]);
        Assert.Equal(new Vector2i(0, 0), cells[1]);
        Assert.Equal(new Vector2i(1, 0), cells[2]);
        Assert.Equal(2, height);
        Assert.Equal(cells.Length, cells.Distinct().Count());
    }

    [Fact]
    public void An_unmapped_slot_grows_the_grid_when_the_figure_is_full()
    {
        List<VisSlot> slots = new()
        {
            VisSlot.BackLeft, VisSlot.Helmet, VisSlot.BackRight,
            VisSlot.HandLeft, VisSlot.Chest, VisSlot.HandRight,
            VisSlot.Shoulder, VisSlot.Legs, VisSlot.Utility,
            VisSlot.Beard,
        };

        Vector2i[] cells = StandLayout.Place(slots, out int height);

        Assert.Equal(4, height);
        Assert.Equal(new Vector2i(0, 3), cells[9]);
    }

    [Fact]
    public void An_empty_stand_still_has_one_row()
    {
        Vector2i[] cells = StandLayout.Place(Array.Empty<VisSlot>(), out int height);

        Assert.Empty(cells);
        Assert.Equal(1, height);
    }

    [Fact]
    public void Slot_by_cell_inverts_the_placement()
    {
        VisSlot[] slots = { VisSlot.Helmet, VisSlot.Legs };
        Vector2i[] cells = StandLayout.Place(slots, out int height);

        int[] byCell = StandLayout.SlotByCell(cells, height);

        Assert.Equal(StandLayout.Width * 3, byCell.Length);
        Assert.Equal(0, byCell[1]);
        Assert.Equal(1, byCell[2 * StandLayout.Width + 1]);
        Assert.Equal(7, byCell.Count(slot => slot == -1));
    }
}
