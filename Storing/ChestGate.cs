using OttoStash.APIs.MUC;

namespace OttoStash.Storing;

/// Whether a vanilla chest may be touched by a store right now.
internal static class ChestGate
{
    /// The rule on plain values, so it can be checked without the engine. The
    /// chest's owner keeps the in-use flag locally and everyone else reads the
    /// synced copy, so both are consulted. The chest in the local player's own
    /// open inventory screen is theirs to store into.
    internal static bool IsOpenElsewhere(bool isLocalPlayersOpenChest, bool ownerInUseFlag, int syncedInUse, bool multiUserChestActive)
    {
        if (multiUserChestActive)
            return false;
        if (isLocalPlayersOpenChest)
            return false;
        return ownerInUseFlag || syncedInUse == 1;
    }

    internal static bool IsOpenElsewhere(Container? chest)
    {
        if (chest == null)
            return false;

        bool isLocalPlayersOpenChest = InventoryGui.instance != null && InventoryGui.instance.m_currentContainer == chest;
        ZDO? zdo = chest.m_nview != null ? chest.m_nview.GetZDO() : null;
        int syncedInUse = zdo != null ? zdo.GetInt(ZDOVars.s_inUse) : 0;

        return IsOpenElsewhere(isLocalPlayersOpenChest, chest.IsInUse(), syncedInUse, MUCCompat.MultiUserChestActive);
    }
}
