namespace AzuAutoStore.Util;

internal static class InventoryMove
{
    /// <summary>
    /// Moves as much of sourceItem as possible to target using fewest AddItem calls.
    /// Returns number of units moved. sourceItem.m_stack is reduced accordingly.
    /// </summary>
    internal static int MoveStackChunked(Inventory target, ItemDrop.ItemData sourceItem)
    {
        if (target == null || sourceItem == null || sourceItem.m_stack <= 0)
            return 0;

        int moved = 0;

        if (target.CanAddItem(sourceItem, sourceItem.m_stack))
        {
            var clone = sourceItem.Clone();
            clone.m_stack = sourceItem.m_stack;

            if (target.AddItem(clone))
            {
                moved += sourceItem.m_stack;
                sourceItem.m_stack = 0;
                return moved;
            }
        }

        while (sourceItem.m_stack > 0)
        {
            int best = FindLargestFittableChunk(target, sourceItem);

            if (best <= 0)
                break;

            var chunk = sourceItem.Clone();
            chunk.m_stack = best;

            if (!target.AddItem(chunk))
            {
                // Extremely rare desync: try single unit to break stalemate, otherwise bail.
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


    private static int FindLargestFittableChunk(Inventory target, ItemDrop.ItemData probe)
    {
        int lo = 1;
        int hi = Mathf.Max(1, probe.m_stack);
        int best = 0;

        while (hi < probe.m_stack && target.CanAddItem(probe, hi)) hi = Math.Min(probe.m_stack, hi << 1);
        if (!target.CanAddItem(probe, hi))
        {
            lo = hi >> 1;
        }

        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
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