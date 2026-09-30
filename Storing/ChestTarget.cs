namespace OttoStash.Storing;

/// A vanilla chest as a store target.
internal sealed class ChestTarget : IStoreTarget
{
    private readonly Container _chest;

    internal ChestTarget(Container chest)
    {
        _chest = chest;
    }

    internal Container Chest => _chest;

    public GameObject GameObject => _chest.gameObject;

    public ZNetView NetView => _chest.m_nview;

    public bool IsOwner()
    {
        return _chest.m_nview.IsOwner();
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

        if (!ChestStore.TryStore(_chest, ref item, fromPlayer: true, announce))
            return 0;

        int moved = before - item.m_stack;
        if (moved <= 0)
            return 0;

        if (item.m_stack == 0)
            inventory.RemoveItem(item);
        else
            inventory.Changed();

        StashLog.Debug($"Stored {moved} {PrefabNames.Of(item)} into {_chest.name}");

        if (!ContainerRegistry.Filled.Contains(this))
            ContainerRegistry.Filled.Add(this);

        return moved;
    }
}
