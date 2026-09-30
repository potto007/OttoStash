using OttoStash.APIs.MUC;

namespace OttoStash.Storing;

/// The two stores the player triggers: the hotkey that empties the inventory
/// into every target in range, and the single-item store from the inventory grid.
internal static class PlayerStore
{
    internal static void StoreAll()
    {
        if (Player.m_localPlayer == null)
            return;
        StashLog.Debug("Trying to store items from player inventory");

        List<IStoreTarget> targets = ContainerRegistry.Nearby(Player.m_localPlayer, PlayerRange.Value);
        int total = StoreRun.Run(targets, target => target.StoreAll(), VanillaChestAccess.Instance, MUCCompat.MultiUserChestActive, StashLog.Debug, StashLog.Error);
        Announce(total);
    }

    internal static void StoreItem(ItemDrop.ItemData item, Inventory inventory)
    {
        if (Player.m_localPlayer == null)
            return;
        if (inventory != Player.m_localPlayer.GetInventory())
            return;
        StashLog.Debug($"Trying to store {item.m_shared.m_name}");

        List<IStoreTarget> targets = ContainerRegistry.Nearby(Player.m_localPlayer, PlayerRange.Value);
        int total = StoreRun.Run(targets, target => target.StoreItem(item, inventory), VanillaChestAccess.Instance, MUCCompat.MultiUserChestActive, StashLog.Debug, StashLog.Error);
        Announce(total);
    }

    // One message for the whole run, and a ping on each target that took something.
    private static void Announce(int total)
    {
        try
        {
            if (total <= 0)
                return;

            Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Stored {total} items from your inventory into nearby containers");

            foreach (IStoreTarget target in ContainerRegistry.Filled)
            {
                ChestEffects.Ping(target.GameObject);
                if (target is ChestTarget && InventoryGui.instance != null)
                    InventoryGui.instance.m_moveItemEffects.Create(target.GameObject.transform.position, Quaternion.identity);
            }
        }
        finally
        {
            ContainerRegistry.Filled.Clear();
        }
    }
}
