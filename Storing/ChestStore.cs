using OttoStash.APIs.MUC;

namespace OttoStash.Storing;

/// Moving one item stack into one vanilla chest, with the rules that apply to
/// every path that does so: the store hotkey, the single-item store, and the
/// ground pickup.
internal static class ChestStore
{
    /// Moves as much of the stack as the chest takes. The stack's count is
    /// reduced by what moved. Announce shows the player why a single item was
    /// refused.
    internal static bool TryStore(Container chest, ref ItemDrop.ItemData item, bool fromPlayer, bool announce)
    {
        StashLog.BuildDebug($"Checking container {(chest != null ? chest.name : "none")}");
        if (chest == null || item.m_shared == null || item.m_dropPrefab == null)
            return false;

        Inventory? inventory = chest.GetInventory();
        if (inventory == null)
            return false;

        string prefab = item.m_dropPrefab.name;

        if (MustHaveExistingItemToPull.Value.IsOn() && !inventory.HaveItem(item.m_shared.m_name))
        {
            if (announce)
            {
                StashLog.Debug($"Skipping {prefab} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{prefab}] is not in nearby containers</color>");
            }
            return false;
        }

        if (!ContainerRules.CanStore(PrefabNames.FromSceneName(chest.transform.root.name), prefab))
        {
            if (announce)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{prefab}] cannot be stored based on configuration settings</color>");
            StashLog.Debug($"{item.m_shared.m_name} cannot be stored based on configuration setting");
            return false;
        }

        if (!chest.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID()))
        {
            StashLog.Debug($"Cannot store items in {chest.name} because the player does not have access.");
            return false;
        }

        if (!MUCCompat.MultiUserChestActive && !chest.m_nview.IsOwner())
        {
            StashLog.Debug($"Cannot store items in {chest.name} because the player is not the owner.");
            return false;
        }

        if (InventoryMove.MoveStackChunked(inventory, item) <= 0)
            return false;

        if (!fromPlayer)
            ChestEffects.Ping(chest.gameObject);

        chest.Save();
        return true;
    }

    /// The pickup range of a chest, from its rules entry or the fallback setting.
    internal static float RangeOf(Container chest)
    {
        if (chest.GetInventory() == null)
            return -1f;
        return ContainerRules.RangeFor(PrefabNames.FromSceneName(chest.transform.root.name), FallbackRange.Value);
    }
}
