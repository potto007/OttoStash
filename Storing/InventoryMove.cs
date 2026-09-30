namespace OttoStash.Storing;

internal static class InventoryMove
{
    /// Moves as much of the stack as the target takes, in as few adds as
    /// possible, and returns the number moved. The source stack is reduced by
    /// that number.
    internal static int MoveStackChunked(Inventory target, ItemDrop.ItemData sourceItem)
    {
        if (target == null || sourceItem == null || sourceItem.m_stack <= 0)
            return 0;

        int moved = 0;

        if (target.CanAddItem(sourceItem, sourceItem.m_stack))
        {
            ItemDrop.ItemData whole = sourceItem.Clone();
            whole.m_stack = sourceItem.m_stack;

            if (target.AddItem(whole))
            {
                moved += sourceItem.m_stack;
                sourceItem.m_stack = 0;
                return moved;
            }
        }

        while (sourceItem.m_stack > 0)
        {
            int best = LargestFittingChunk(target, sourceItem);
            if (best <= 0)
                break;

            ItemDrop.ItemData chunk = sourceItem.Clone();
            chunk.m_stack = best;

            if (!target.AddItem(chunk))
            {
                // A rare disagreement between CanAddItem and AddItem: try one unit, then give up.
                chunk.m_stack = 1;
                if (!target.AddItem(chunk))
                    break;
                best = 1;
            }

            moved += best;
            sourceItem.m_stack -= best;
        }

        return moved;
    }

    // Binary search for the largest part of the stack the inventory still takes.
    // Fitting is monotonic in the amount, so the search is sound from 1 up to the
    // whole stack. An earlier version started the search at half the stack and
    // so moved nothing when less than half fitted.
    private static int LargestFittingChunk(Inventory target, ItemDrop.ItemData probe)
    {
        int lo = 1;
        int hi = probe.m_stack;
        int best = 0;

        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            if (target.CanAddItem(probe, mid))
            {
                best = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return best;
    }
}
