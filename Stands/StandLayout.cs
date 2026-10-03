namespace OttoStash.Stands;

/// Where each armor stand slot sits in the panel grid. The grid is three wide and
/// drawn as a figure: head on top, hands either side of the chest, cape, legs and
/// utility item along the bottom. Cells no slot claims are hidden by the grid patch.
internal static class StandLayout
{
    internal const int Width = 3;

    private static readonly Dictionary<VisSlot, Vector2i> Preferred = new()
    {
        [VisSlot.BackLeft] = new Vector2i(0, 0),
        [VisSlot.Helmet] = new Vector2i(1, 0),
        [VisSlot.BackRight] = new Vector2i(2, 0),
        [VisSlot.HandLeft] = new Vector2i(0, 1),
        [VisSlot.Chest] = new Vector2i(1, 1),
        [VisSlot.HandRight] = new Vector2i(2, 1),
        [VisSlot.Shoulder] = new Vector2i(0, 2),
        [VisSlot.Legs] = new Vector2i(1, 2),
        [VisSlot.Utility] = new Vector2i(2, 2),
    };

    /// One cell per slot, in slot order. A slot whose preferred cell is taken, or
    /// that has no preferred cell, takes the first free cell reading left to right,
    /// top to bottom, and the grid grows a row when it must. Height is at least one.
    internal static Vector2i[] Place(IReadOnlyList<VisSlot> slots, out int height)
    {
        Vector2i[] cells = new Vector2i[slots.Count];
        HashSet<Vector2i> taken = new();
        List<int> unplaced = new();

        for (int i = 0; i < slots.Count; i++)
        {
            if (Preferred.TryGetValue(slots[i], out Vector2i cell) && taken.Add(cell))
                cells[i] = cell;
            else
                unplaced.Add(i);
        }

        foreach (int i in unplaced)
        {
            int index = 0;
            Vector2i cell;
            do
            {
                cell = new Vector2i(index % Width, index / Width);
                index++;
            } while (!taken.Add(cell));

            cells[i] = cell;
        }

        height = 1;
        foreach (Vector2i cell in cells)
            height = Math.Max(height, cell.y + 1);

        return cells;
    }

    /// The slot index at each cell, by `y * Width + x`, or -1 for a cell no slot claims.
    internal static int[] SlotByCell(Vector2i[] cells, int height)
    {
        int[] slots = new int[Width * height];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = -1;
        for (int i = 0; i < cells.Length; i++)
            slots[cells[i].y * Width + cells[i].x] = i;
        return slots;
    }
}
