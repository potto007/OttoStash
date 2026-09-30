using OttoStash.APIs;

namespace OttoStash.Storing;

/// A Makail ItemDrawers drawer as a store target. The drawer takes whole stacks
/// through its own API and reports nothing back, so the count moved is always
/// zero here even when items went in.
internal sealed class MkzDrawerTarget : IStoreTarget
{
    private readonly MkzItemDrawers_API.Drawer _drawer;

    internal MkzDrawerTarget(MkzItemDrawers_API.Drawer drawer)
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

        List<ItemDrop.ItemData> items = Player.m_localPlayer.GetInventory().GetAllItems();
        int planned = 0;

        for (int i = items.Count - 1; i >= 0; --i)
        {
            ItemDrop.ItemData item = items[i];
            if (item == null || PlayerItemFilter.StaysWithPlayer(item))
                continue;

            string? prefab = PrefabOf(item);
            if (prefab == null || !Accepts(item, prefab, announce: false))
                continue;

            int want = item.m_stack;
            if (want <= 0)
                continue;

            _drawer.Add(prefab, want);
            planned += want;
        }

        if (planned > 0 && !ContainerRegistry.Filled.Contains(this))
            ContainerRegistry.Filled.Add(this);

        return 0;
    }

    public int StoreItem(ItemDrop.ItemData item, Inventory playerInventory)
    {
        if (Player.m_localPlayer == null || item == null)
            return 0;
        if (PlayerItemFilter.StaysWithPlayer(item))
            return 0;

        string? prefab = PrefabOf(item);
        if (prefab == null || !Accepts(item, prefab, announce: true))
            return 0;

        int want = item.m_stack;
        if (want <= 0)
            return 0;

        _drawer.Add(prefab, want);

        if (!ContainerRegistry.Filled.Contains(this))
            ContainerRegistry.Filled.Add(this);

        return 0;
    }

    private static string? PrefabOf(ItemDrop.ItemData item)
    {
        string? prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : null;
        return string.IsNullOrEmpty(prefab) ? null : prefab;
    }

    private bool Accepts(ItemDrop.ItemData item, string prefab, bool announce)
    {
        if (!ContainerRules.CanStore(PrefabNames.FromSceneName(_drawer.gameObject.name), prefab))
            return false;

        if (MustHaveExistingItemToPull.Value.IsOn() && !string.Equals(_drawer.Prefab, prefab))
        {
            if (announce)
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{prefab}] is not in nearby drawers</color>");
            return false;
        }

        return _drawer.Accepts(prefab);
    }
}
