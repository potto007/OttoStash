namespace OttoStash.Storing;

/// The items in the player's inventory that a store never takes.
internal static class PlayerItemFilter
{
    /// True when the item stays with the player: it is equipped, sits on the
    /// hotbar with that setting on, sits in an AzuEPI quick slot, or is
    /// favorited by name or by slot.
    internal static bool StaysWithPlayer(ItemDrop.ItemData item)
    {
        if (item.m_equipped)
        {
            StashLog.Debug($"Skipping equipped item {PrefabNames.Of(item)}");
            return true;
        }

        if (IsOnHotbar(item) && PlayerIgnoreHotbar.Value.IsOn())
        {
            StashLog.Debug($"Skipping item {PrefabNames.Of(item)} because it is in the hotbar");
            return true;
        }

        if (AzuExtendedPlayerInventory.API.IsLoaded() && AzuExtendedPlayerInventory.API.GetQuickSlotsItems().Any(quickSlotItem => quickSlotItem.m_gridPos == item.m_gridPos))
        {
            StashLog.Debug($"Skipping item {PrefabNames.Of(item)} because it is in your quick slots");
            return true;
        }

        if (PlayerFavorites.For(Player.m_localPlayer.GetPlayerID()).IsItemOrSlotFavorited(item))
        {
            StashLog.Debug($"Skipping favorited item/slot {PrefabNames.Of(item)}");
            return true;
        }

        return false;
    }

    // The first inventory row is the hotbar.
    private static bool IsOnHotbar(ItemDrop.ItemData item)
    {
        return item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0;
    }
}
