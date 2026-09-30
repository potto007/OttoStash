using Backpacks;

namespace OttoStash.Storing;

/// A Backpacks-mod backpack in the player's inventory as a store target. The
/// player always owns it, and the ping lands on the player.
internal sealed class BackpackTarget : IStoreTarget
{
    private readonly ItemContainer _backpack;

    internal BackpackTarget(ItemContainer backpack)
    {
        _backpack = backpack;
    }

    public GameObject GameObject => Player.m_localPlayer.gameObject;

    public ZNetView NetView => Player.m_localPlayer.m_nview;

    public bool IsOwner()
    {
        return true;
    }

    public int StoreAll()
    {
        if (Player.m_localPlayer == null)
            return 0;

        int total = 0;
        Inventory inventory = Player.m_localPlayer.GetInventory();
        List<ItemDrop.ItemData> items = inventory.GetAllItems();

        for (int i = items.Count - 1; i >= 0; --i)
        {
            ItemDrop.ItemData item = items[i];
            if (PlayerItemFilter.StaysWithPlayer(item))
                continue;

            total += StoreOne(item, inventory, announce: false);
        }

        return total;
    }

    public int StoreItem(ItemDrop.ItemData item, Inventory playerInventory)
    {
        if (Player.m_localPlayer == null || item == null)
            return 0;
        if (PlayerItemFilter.StaysWithPlayer(item))
            return 0;

        return StoreOne(item, playerInventory ?? Player.m_localPlayer.GetInventory(), announce: true);
    }

    private int StoreOne(ItemDrop.ItemData item, Inventory inventory, bool announce)
    {
        StashLog.Debug($"Checking item {PrefabNames.Of(item)}");
        int before = item.m_stack;

        if (!TryStore(ref item, announce))
            return 0;

        int moved = before - item.m_stack;
        if (moved <= 0)
            return 0;

        if (item.m_stack == 0)
            inventory.RemoveItem(item);
        else
            inventory.Changed();

        StashLog.Debug($"Stored {moved} {PrefabNames.Of(item)} into {_backpack.Item.m_dropPrefab.name}");

        if (!ContainerRegistry.Filled.Contains(this))
            ContainerRegistry.Filled.Add(this);

        return moved;
    }

    // The backpack API takes one unit at a time.
    private bool TryStore(ref ItemDrop.ItemData item, bool announce)
    {
        string backpackName = _backpack.Item.m_dropPrefab.name;
        StashLog.Debug($"Checking container {backpackName}");
        if (item.m_shared == null)
            return false;

        if (MustHaveExistingItemToPull.Value.IsOn() && !_backpack.Inventory.HaveItem(item.m_shared.m_name))
        {
            if (announce)
            {
                StashLog.Debug($"Skipping {item.m_dropPrefab.name} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} is not in nearby containers</color>");
            }
            return false;
        }

        if (!ContainerRules.CanStore(PrefabNames.FromSceneName(backpackName), item.m_dropPrefab.name))
            return false;

        bool changed = false;
        while (item.m_stack > 1 && _backpack.Inventory.CanAddItem(item, 1))
        {
            ItemDrop.ItemData one = item.Clone();
            one.m_stack = 1;
            API.AddItemToBackpack(_backpack.Item, one);
            item.m_stack--;
            changed = true;
            StashLog.Debug($"Auto storing {item.m_dropPrefab.name} in {backpackName}");
        }

        if (item.m_stack == 1 && _backpack.Inventory.CanAddItem(item, 1))
        {
            ItemDrop.ItemData last = item.Clone();
            item.m_stack = 0;
            API.AddItemToBackpack(_backpack.Item, last);
            changed = true;
        }

        if (!changed)
            return false;

        try
        {
            _backpack.Save();
        }
        catch
        {
            // The backpack mod saves on its own schedule; a failed early save is harmless.
        }

        return true;
    }
}
