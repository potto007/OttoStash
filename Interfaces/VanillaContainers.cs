using OttoStash.APIs.MUC;

namespace OttoStash.Interfaces;

public class VanillaContainers(Container _container) : IContainer
{
    public GameObject gameObject => _container.gameObject;
    public ZNetView m_nview => _container.m_nview;

    public static bool CantStoreFavorite(ItemDrop.ItemData item, UserConfig playerConfig)
    {
        return playerConfig.IsItemNameOrSlotFavorited(item);
    }

    internal static void LogDebug(string data)
    {
        OttoStashLogger.LogDebug(data);
    }

    public int TryStore()
    {
        if (!Player.m_localPlayer) return 0;

        int total = 0;
        Inventory? inv = Player.m_localPlayer.GetInventory();
        List<ItemDrop.ItemData>? items = inv.GetAllItems();

        for (int j = items.Count - 1; j >= 0; --j)
        {
            ItemDrop.ItemData? item = items[j];
            if (item.m_equipped)
            {
                LogDebug($"Skipping equipped item {item.m_dropPrefab.name}");
                continue;
            }

            // If the item.m_gridPos.x is 1-8 and item.m_gridPos.y is 0 (the first row), then do not store the item if _playerIgnoreHotbar is true
            if (item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0 && PlayerIgnoreHotbar.Value.IsOn())
            {
                LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in the hotbar");
                continue;
            }

            if (AzuExtendedPlayerInventory.API.IsLoaded())
            {
                // Get quick slot positions
                List<ItemDrop.ItemData> quickSlotsItems = AzuExtendedPlayerInventory.API.GetQuickSlotsItems();


                // Check if the item is in the quick slots
                if (quickSlotsItems.Any(quickSlotItem => quickSlotItem.m_gridPos == item.m_gridPos))
                {
                    LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in your quick slots");
                    continue;
                }
            }

            if (CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID())))
            {
                LogDebug($"Skipping favorited item/slot {item.m_dropPrefab.name}");
                continue;
            }

            LogDebug($"Checking item {item.m_dropPrefab.name}");
            int originalAmount = item.m_stack;

            if (!TryStore(_container, ref item, fromPlayer: true))
                continue;

            int moved = originalAmount - item.m_stack;
            if (moved <= 0) continue;

            total += moved;

            if (item.m_stack == 0)
                inv.RemoveItem(item);
            else
                inv.Changed();

            LogDebug($"Stored {moved} {item.m_dropPrefab.name} into {_container.name}");

            if (!Boxes.ContainersToPing.Contains(this))
                Boxes.ContainersToPing.Add(this);
        }

        return total;
    }

    public int TryStoreThisItem(ItemDrop.ItemData? singleItemData = null, Inventory? playerInventory = null)
    {
        if (!Player.m_localPlayer) return 0;

        Inventory inv = playerInventory ?? Player.m_localPlayer.GetInventory();
        if (singleItemData == null) return 0;

        int total = 0;
        ItemDrop.ItemData item = singleItemData;

        if (item.m_equipped)
        {
            LogDebug($"Skipping equipped item {item.m_dropPrefab.name}");
            return 0;
        }

        // If the item.m_gridPos.x is 1-8 and item.m_gridPos.y is 0 (the first row), then do not store the item if _playerIgnoreHotbar is true
        if (item.m_gridPos.x is >= 0 and <= 8 && item.m_gridPos.y == 0 && PlayerIgnoreHotbar.Value.IsOn())
        {
            LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in the hotbar");
            return 0;
        }

        if (AzuExtendedPlayerInventory.API.IsLoaded())
        {
            // Get quick slot positions
            List<ItemDrop.ItemData> quickSlotsItems = AzuExtendedPlayerInventory.API.GetQuickSlotsItems();


            // Check if the item is in the quick slots
            if (quickSlotsItems.Any(quickSlotItem => quickSlotItem.m_gridPos == item.m_gridPos))
            {
                LogDebug($"Skipping item {item.m_dropPrefab.name} because it is in your quick slots");
                return 0;
            }
        }

        if (CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID())))
        {
            LogDebug($"Skipping favorited item/slot {item.m_dropPrefab.name}");
            return 0;
        }

        LogDebug($"Checking item {item.m_dropPrefab.name}");
        int originalAmount = item.m_stack;

        if (!Functions.TryStore(_container, ref item, fromPlayer: true, singleItemData: true))
            return 0;

        int moved = originalAmount - item.m_stack;
        if (moved <= 0) return 0;

        total += moved;

        if (item.m_stack == 0)
            inv.RemoveItem(item);
        else
            inv.Changed();

        LogDebug($"Stored {moved} {item.m_dropPrefab.name} into {_container.name}");

        if (!Boxes.ContainersToPing.Contains(this))
            Boxes.ContainersToPing.Add(this);

        return total;
    }

    internal bool TryStore(Container nearbyContainer, ref ItemDrop.ItemData item, bool fromPlayer = false)
    {
        if (!nearbyContainer) return false;
        if (!MiscFunctions.CheckItemSharedIntegrity(item)) return false;

        var inv = nearbyContainer.GetInventory();
        if (inv == null) return false;

        if (MustHaveExistingItemToPull.Value.IsOn() && !inv.HaveItem(item.m_shared.m_name))
            return false;

        if (!item.m_dropPrefab) return false;

        if (!Boxes.CanItemBeStored(MiscFunctions.GetPrefabName(nearbyContainer.transform.root.name),
                item.m_dropPrefab.name))
            return false;

        if (!nearbyContainer.CheckAccess(Game.instance.GetPlayerProfile().GetPlayerID()))
            return false;

        if (!MUCCompat.MultiUserChestActive && !nearbyContainer.m_nview.IsOwner())
            return false;

        if (!MUCCompat.MultiUserChestActive && Functions.IsContainerBeingUsed(nearbyContainer))
            return false;

        int moved = InventoryMove.MoveStackChunked(inv, item);
        bool changed = moved > 0;

        if (changed)
        {
            if (!fromPlayer)
                Functions.PingContainer(nearbyContainer.gameObject);

            nearbyContainer.Save();
        }

        return changed;
    }


    public bool IsOwner() => _container.m_nview.IsOwner();

    public Inventory GetInventory()
    {
        return _container.GetInventory();
    }

    public static VanillaContainers Create(Container container) => new(container);
}