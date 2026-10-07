using EpicLootApi = EpicLootAPI.EpicLoot;

namespace OttoStash.Pulling;

/// Offers the nearby containers to Epic Loot's enchanting table as an extra
/// inventory, under the table's own pull rules.
internal static class EpicLootCompat
{
    private const string TablePrefabName = "piece_enchantingtable";

    internal static void Init()
    {
        if (!EpicLootApi.IsLoaded())
            return;
        EpicLootApi.RegisterInventoryProvider(ModGUID, GetItems, CountItem, RemoveItem, RemoveExactItem);
    }

    private static IReadOnlyList<IPullSource> Nearby()
    {
        Player player = Player.m_localPlayer;
        return Pull.Active(player) ? Pull.Sources(player) : Array.Empty<IPullSource>();
    }

    private static bool Allowed(IPullSource source, ItemDrop.ItemData item)
    {
        return item.m_dropPrefab != null && ContainerRules.CanPull(source.RuleName, item.m_dropPrefab.name, TablePrefabName);
    }

    private static readonly Dictionary<string, string> PrefabBySharedName = new();

    /// Epic Loot names items by their shared name; the pull rules use prefab names.
    private static string? PrefabOf(string sharedName)
    {
        if (PrefabBySharedName.Count == 0 && ObjectDB.instance != null)
        {
            foreach (GameObject prefab in ObjectDB.instance.m_items)
            {
                string? name = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name : null;
                if (name != null && !PrefabBySharedName.ContainsKey(name))
                    PrefabBySharedName[name] = prefab!.name;
            }
        }

        return PrefabBySharedName.TryGetValue(sharedName, out string prefabName) ? prefabName : null;
    }

    // Epic Loot needs the live instances, not copies, so magic data survives the round trip.
    private static List<ItemDrop.ItemData> GetItems()
    {
        List<ItemDrop.ItemData> items = new();
        foreach (IPullSource source in Nearby())
        {
            Inventory? inventory = source.Inventory;
            if (inventory == null)
                continue;
            items.AddRange(inventory.GetAllItems().Where(item => item != null && Allowed(source, item)));
        }

        return items;
    }

    private static int CountItem(string itemName)
    {
        string? prefab = PrefabOf(itemName);
        return prefab == null ? 0 : PullStock.Available(Nearby(), itemName, prefab, -1, TablePrefabName, LeaveOneItem.Value.IsOn());
    }

    private static int RemoveItem(string itemName, int amount)
    {
        string? prefab = PrefabOf(itemName);
        return prefab == null ? 0 : PullStock.Take(Nearby(), itemName, prefab, -1, TablePrefabName, LeaveOneItem.Value.IsOn(), amount);
    }

    // Match by reference: a name match would consume the wrong enchanted item.
    private static int RemoveExactItem(ItemDrop.ItemData item, int amount)
    {
        foreach (IPullSource source in Nearby())
        {
            Inventory? inventory = source.Inventory;
            if (inventory == null || !inventory.GetAllItems().Contains(item))
                continue;

            int take = Math.Min(item.m_stack, amount);
            (source as ChestSource)?.ClaimOwnership();
            if (inventory.RemoveItem(item, take))
                return take;
        }

        return 0;
    }
}
