namespace OttoStash.Reclaiming;

/// Undo for the last reclaim from the Reclaim tab, within the grace period.
internal static class ReclaimUndo
{
    private static ItemDrop.ItemData? _lastItem;
    private static List<ReclaimYield> _lastEntries = new();
    private static float _recordedAt = float.MinValue;

    internal static bool CanUndo => _lastItem != null && UndoRecycleGracePeriodSeconds.Value > 0 && Time.time - _recordedAt <= UndoRecycleGracePeriodSeconds.Value;

    internal static void Record(ItemDrop.ItemData item, List<ReclaimYield> entries)
    {
        _lastItem = item.Clone();
        _lastEntries = entries.Where(entry => entry.Amount > 0 && !entry.InitialRecipeHadZero).ToList();
        _recordedAt = Time.time;
    }

    /// Takes the returned resources back and restores the item. Checks every
    /// resource is still there first, and puts them back if the item will not fit.
    internal static void TryUndo()
    {
        Player player = Player.m_localPlayer;
        if (player == null)
            return;

        if (!CanUndo)
        {
            player.Message(MessageHud.MessageType.Center, ReclaimText.Localize("$ottostash_reclaim_undo_expired"));
            Clear();
            return;
        }

        Inventory inventory = player.GetInventory();
        if (_lastEntries.Any(entry => inventory.CountItems(entry.RecipeItemData.m_shared.m_name) < entry.Amount))
        {
            player.Message(MessageHud.MessageType.Center, ReclaimText.Localize("$ottostash_reclaim_undo_failed"));
            Clear();
            return;
        }

        foreach (ReclaimYield entry in _lastEntries)
            inventory.RemoveItem(entry.RecipeItemData.m_shared.m_name, entry.Amount);

        ItemDrop.ItemData item = _lastItem!;
        ItemDrop.ItemData? restored = item.m_dropPrefab != null
            ? inventory.AddItem(item.m_dropPrefab.name, item.m_stack, item.m_quality, item.m_variant, item.m_crafterID, item.m_crafterName, false)
            : null;

        if (restored == null)
        {
            foreach (ReclaimYield entry in _lastEntries)
            {
                if (inventory.AddItem(entry.Prefab.name, entry.Amount, entry.Quality, entry.Variant, 0, "", false) == null)
                    StashLog.Error($"Undo rollback could not return {entry.Amount}x {entry.Prefab.name} to the inventory.");
            }

            player.Message(MessageHud.MessageType.Center, ReclaimText.Localize("$ottostash_reclaim_undo_failed"));
            Clear();
            return;
        }

        player.Message(MessageHud.MessageType.Center, ReclaimText.Localize("$ottostash_reclaim_undo_success"));
        Clear();
    }

    internal static void Clear()
    {
        _lastItem = null;
        _lastEntries.Clear();
    }
}
