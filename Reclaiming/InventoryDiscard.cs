using EpicLootAPI;
using Object = UnityEngine.Object;

namespace OttoStash.Reclaiming;

/// Discard from the inventory: press DiscardHotkey while dragging an item to
/// destroy it and get its resources back. Bulk trashing uses the same path.
[HarmonyPatch]
internal static class InventoryDiscard
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateItemDrag))]
    private static void InventoryGuiUpdateItemDragPostfix(InventoryGui __instance)
    {
        ItemDrop.ItemData? item = __instance.m_dragItem;
        if (!DiscardAllowed || !DiscardHotkey.Value.IsDown() || item == null || __instance.m_dragInventory == null
            || !__instance.m_dragInventory.ContainsItem(item))
            return;

        Discard(item, __instance.m_dragAmount, __instance.m_dragInventory);

        // Clears the whole drag state; destroying m_dragGo alone leaves m_dragItem
        // set, and the next Delete press would discard from the stack again.
        __instance.SetupDragItem(null, null, 1);
        __instance.UpdateCraftingPanel();
    }

    /// Removes amount of the item and returns its resources, scaled by
    /// ReturnResources. What does not fit in the inventory drops at the player's feet.
    internal static void Discard(ItemDrop.ItemData item, int amount, Inventory inventory)
    {
        string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
        if (ReclaimRules.IsExcludedInInventory(prefabName))
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"{prefabName} is excluded by the inventory section of potto007.OttoStash.Reclaim.yml");
            return;
        }

        if (DiscardReturnResources.Value > 0 && !ReturnResources(item, amount))
            return;

        if (amount == item.m_stack)
        {
            Player.m_localPlayer.RemoveEquipAction(item);
            Player.m_localPlayer.UnequipItem(item, false);
            inventory.RemoveItem(item);
        }
        else
        {
            inventory.RemoveItem(item, amount);
        }
    }

    /// False when the item must be kept: an enchantment or gem material whose
    /// recipe the player does not know.
    private static bool ReturnResources(ItemDrop.ItemData item, int amount)
    {
        Recipe? recipe = ObjectDB.instance.GetRecipe(item);
        if (recipe == null || (DiscardReturnUnknownResources.Value.IsOff() && !Player.m_localPlayer.IsRecipeKnown(item.m_shared.m_name)))
            return true;

        List<Piece.Requirement> requirements = recipe.m_resources.ToList();
        if (DiscardReturnEnchantedResources.Value.IsOn() && !AddEnchantedRequirements(item, requirements))
        {
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, ReclaimText.Localize("$ottostash_reclaim_no_material_recipes"));
            return false;
        }

        int crafts = amount / recipe.m_amount;
        for (int craft = 0; craft < crafts; craft++)
        {
            foreach (Piece.Requirement requirement in requirements)
            {
                foreach (int level in ReclaimMath.SpentQualityLevels(requirement.m_upgraderResource, item.m_shared.m_maxQuality, item.m_quality))
                    Give(requirement, Mathf.RoundToInt(requirement.GetAmount(level) * DiscardReturnResources.Value));
            }
        }

        return true;
    }

    private static bool AddEnchantedRequirements(ItemDrop.ItemData item, List<Piece.Requirement> requirements)
    {
        if (ReclaimFeature.HasEpicLoot && item.IsMagicItem() && item.TryGetRarity(out var rarity))
        {
            foreach (var cost in item.GetEnchantCosts(rarity))
            {
                ItemDrop? costItem = ObjectDB.instance.GetItemPrefab(cost.Item)?.GetComponent<ItemDrop>();
                if (costItem == null)
                    continue;
                Recipe? costRecipe = ObjectDB.instance.GetRecipe(costItem.m_itemData);
                if (!KnownForDiscard(costItem.m_itemData, costRecipe))
                    return false;
                requirements.Add(new Piece.Requirement { m_amount = costRecipe != null ? costRecipe.m_amount : cost.Amount, m_resItem = costItem });
            }
        }

        if (Jewelcrafting.API.IsLoaded())
        {
            foreach (var gem in Jewelcrafting.API.GetGems(item))
            {
                ItemDrop? gemItem = gem == null ? null : ObjectDB.instance.GetItemPrefab(gem.gemPrefab)?.GetComponent<ItemDrop>();
                if (gemItem == null)
                    continue;
                Recipe? gemRecipe = ObjectDB.instance.GetRecipe(gemItem.m_itemData);
                if (!KnownForDiscard(gemItem.m_itemData, gemRecipe))
                    return false;
                requirements.Add(new Piece.Requirement { m_amount = gemRecipe != null ? gemRecipe.m_amount : gemItem.m_itemData.m_stack, m_resItem = gemItem });
            }
        }

        return true;
    }

    /// Discard asks for a known recipe even when the material has none, as it always has.
    private static bool KnownForDiscard(ItemDrop.ItemData material, Recipe? recipe)
    {
        if (DiscardReturnUnknownResources.Value.IsOn())
            return true;
        Player player = Player.m_localPlayer;
        return recipe != null && player.IsRecipeKnown(material.m_shared.m_name) && player.m_knownMaterial.Contains(material.m_shared.m_name);
    }

    private static void Give(Piece.Requirement requirement, int amount)
    {
        GameObject? prefab = ReclaimItems.FindItemPrefab(requirement.m_resItem.m_itemData);
        if (prefab == null || ReclaimRules.IsExcludedInInventory(global::Utils.GetPrefabName(prefab)))
            return;

        Player player = Player.m_localPlayer;
        Inventory inventory = player.GetInventory();
        ItemDrop.ItemData resource = requirement.m_resItem.m_itemData;
        while (amount > 0)
        {
            int stack = Mathf.Min(resource.m_shared.m_maxStackSize, amount);
            amount -= stack;

            ItemDrop.ItemData? added = ApplyCraftedBy.Value.IsOn()
                ? inventory.AddItem(prefab.name, stack, resource.m_quality, resource.m_variant, player.GetPlayerID(), player.GetPlayerName(), false)
                : inventory.AddItem(prefab.name, stack, resource.m_quality, resource.m_variant, 0, "", false);
            if (added != null)
                continue;

            Transform transform = player.transform;
            ItemDrop drop = Object.Instantiate(prefab, transform.position + transform.forward + transform.up, transform.rotation).GetComponent<ItemDrop>();
            drop.m_itemData = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            drop.m_itemData.m_dropPrefab = prefab;
            drop.m_itemData.m_stack = stack;
            drop.Save();
        }
    }
}
