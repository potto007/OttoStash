using AzuAutoStore.APIs;

namespace AzuAutoStore.Interfaces;

public sealed class mkzDrawer(MkzItemDrawers_API.Drawer _drawer) : IContainer
{
    private static bool CantStoreFavorite(ItemDrop.ItemData item, UserConfig playerConfig) => playerConfig.IsItemNameOrSlotFavorited(item);

    internal static void LogDebug(string s) => AzuAutoStoreLogger.LogDebug(s);

    public GameObject gameObject => _drawer.gameObject;
    public ZNetView m_nview => _drawer.m_nview;
    public bool IsOwner() => true;

    public static mkzDrawer Create(MkzItemDrawers_API.Drawer d) => new(d);

    public int TryStore()
    {
        if (!Player.m_localPlayer) return 0;

        Inventory? inv = Player.m_localPlayer.GetInventory();
        List<ItemDrop.ItemData>? items = inv.GetAllItems();

        int planned = 0;

        for (int j = items.Count - 1; j >= 0; --j)
        {
            ItemDrop.ItemData? item = items[j];
            if (item == null) continue;
            if (item.m_equipped) continue;

            if (item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0 && PlayerIgnoreHotbar.Value.IsOn())
                continue;

            if (AzuExtendedPlayerInventory.API.IsLoaded())
            {
                List<ItemDrop.ItemData> quick = AzuExtendedPlayerInventory.API.GetQuickSlotsItems();
                if (quick.Any(q => q.m_gridPos == item.m_gridPos)) continue;
            }

            if (CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID())))
                continue;

            string prefab = item.m_dropPrefab ? item.m_dropPrefab.name : null;
            if (string.IsNullOrEmpty(prefab)) continue;

            if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(_drawer.gameObject.name), prefab))
                continue;

            if (MustHaveExistingItemToPull.Value.IsOn())
            {
                if (!string.Equals(_drawer.Prefab, prefab)) continue;
            }

            if (!_drawer.Accepts(prefab)) continue;

            int want = item.m_stack;
            if (want <= 0) continue;

            _drawer.Add(prefab, want);
            planned += want;
        }

        if (planned > 0 && !Boxes.ContainersToPing.Contains(this))
            Boxes.ContainersToPing.Add(this);

        return 0;
    }

    public int TryStoreThisItem(ItemDrop.ItemData singleItemData = null, Inventory playerInventory = null)
    {
        if (!Player.m_localPlayer || singleItemData == null) return 0;

        ItemDrop.ItemData item = singleItemData;

        if (item.m_equipped) return 0;

        if (item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0 && PlayerIgnoreHotbar.Value.IsOn())
            return 0;

        if (AzuExtendedPlayerInventory.API.IsLoaded())
        {
            List<ItemDrop.ItemData> quick = AzuExtendedPlayerInventory.API.GetQuickSlotsItems();
            if (quick.Any(q => q.m_gridPos == item.m_gridPos)) return 0;
        }

        if (CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID())))
            return 0;

        string prefab = item.m_dropPrefab ? item.m_dropPrefab.name : null;
        if (string.IsNullOrEmpty(prefab)) return 0;

        if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(_drawer.gameObject.name), prefab))
            return 0;

        if (MustHaveExistingItemToPull.Value.IsOn())
        {
            if (!string.Equals(_drawer.Prefab, prefab))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"<color=red>{item.m_shared.m_name} [{prefab}] is not in nearby drawers</color>");
                return 0;
            }
        }

        if (!_drawer.Accepts(prefab)) return 0;

        int want = item.m_stack;
        if (want <= 0) return 0;

        _drawer.Add(prefab, want);

        if (!Boxes.ContainersToPing.Contains(this))
            Boxes.ContainersToPing.Add(this);

        return 0;
    }
}