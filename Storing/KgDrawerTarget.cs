using OttoStash.APIs;

namespace OttoStash.Storing;

/// A kg ItemDrawers drawer as a store target. A drawer holds one prefab at one
/// quality, and the drawer mod arbitrates access itself.
internal sealed class KgDrawerTarget : IStoreTarget
{
    private readonly ItemDrawers_API.Drawer _drawer;

    internal KgDrawerTarget(ItemDrawers_API.Drawer drawer)
    {
        _drawer = drawer;
    }

    public GameObject GameObject => _drawer.gameObject;

    public ZNetView NetView => _drawer.m_nview;

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
            if (item.m_dropPrefab.name != _drawer.Prefab)
                continue;
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
        if (item.m_dropPrefab.name != _drawer.Prefab)
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

        StashLog.Debug($"Stored {moved} {PrefabNames.Of(item)} into {_drawer.gameObject.name}");

        if (!ContainerRegistry.Filled.Contains(this))
            ContainerRegistry.Filled.Add(this);

        return moved;
    }

    private bool TryStore(ref ItemDrop.ItemData item, bool announce)
    {
        StashLog.Debug($"Checking container {_drawer.gameObject.name}");
        if (item.m_shared == null)
            return false;

        if (MustHaveExistingItemToPull.Value.IsOn() && _drawer.Prefab != item.m_dropPrefab.name)
        {
            if (announce)
            {
                StashLog.Debug($"Skipping {item.m_dropPrefab.name} because it is not in the container");
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} is not in nearby containers</color>");
            }
            return false;
        }

        if (!ContainerRules.CanStore(PrefabNames.FromSceneName(_drawer.gameObject.name), item.m_dropPrefab.name))
            return false;

        if (_drawer.Quality != item.m_quality)
        {
            StashLog.Debug($"Container {_drawer.gameObject.name} has {_drawer.Prefab} that is different quality than what you're trying to store. Container Quality: {_drawer.Quality} | Item Quality: {item.m_quality}");
            return false;
        }

        bool changed = false;
        while (item.m_stack > 1 && _drawer.Prefab == item.m_dropPrefab.name)
        {
            _drawer.Add(1, item.m_quality);
            item.m_stack--;
            changed = true;
            StashLog.Debug($"Auto storing {item.m_dropPrefab.name} in {_drawer.gameObject.name}");
        }

        if (item.m_stack == 1 && _drawer.Prefab == item.m_dropPrefab.name)
        {
            _drawer.Add(1, item.m_quality);
            item.m_stack = 0;
            changed = true;
        }

        return changed;
    }
}
