using System.Text;
using EpicLootAPI;

namespace OttoStash.Reclaiming;

/// Turns items back into the resources they were made from: the analysis that
/// decides whether and what, and the inventory change that does it.
internal static class Reclaimer
{
    private static readonly Dictionary<string, List<Recipe>> RecipeCache = new();

    internal static void BuildRecipeCache(ObjectDB objectDb)
    {
        RecipeCache.Clear();
        foreach (Recipe recipe in objectDb.m_recipes)
        {
            string? name = recipe?.m_item?.m_itemData?.m_shared?.m_name;
            if (name == null)
                continue;
            if (!RecipeCache.TryGetValue(name, out List<Recipe> list))
            {
                list = new List<Recipe>();
                RecipeCache[name] = list;
            }

            list.Add(recipe!);
        }

        StashLog.Debug($"Built the reclaim recipe cache with {RecipeCache.Count} entries");
    }

    /// Reclaim All on a container: every item the rules allow, reporting the ones
    /// that could not be reclaimed.
    internal static void ReclaimInventory(Inventory inventory, string containerName, Player player)
    {
        List<ItemDrop.ItemData> snapshot = new(inventory.GetAllItems());
        List<ReclaimAnalysis> analyses = new();
        foreach (ItemDrop.ItemData item in snapshot)
        {
            ReclaimAnalysis analysis = new(item);
            analyses.Add(analysis);
            string prefabName = global::Utils.GetPrefabName(item.m_dropPrefab);
            if (!ReclaimRules.IsExcludedInContainer(containerName, prefabName))
                ReclaimOne(analysis, inventory, player);

            if (analysis.ShouldErrorDumpAnalysis || DebugAlwaysDumpAnalysisContext.Value.IsOn())
                analysis.Dump();
        }

        StringBuilder report = new();
        foreach (ReclaimAnalysis analysis in analyses.Where(analysis => analysis.RecyclingImpediments.Count > 0))
        {
            report.AppendLine(ReclaimText.Localize("$ottostash_reclaim_could_not_recycle", ReclaimText.Localize(analysis.Item.m_shared.m_name), analysis.RecyclingImpediments.Count.ToString()));
            foreach (string impediment in analysis.RecyclingImpediments)
                report.AppendLine(impediment);
        }

        if (report.Length == 0 || NotifyOnSalvagingImpediments.Value.IsOff())
            return;
        MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, report.ToString());
    }

    internal static List<ReclaimAnalysis> AnalyzeInventory(Inventory inventory, Player player)
    {
        List<ReclaimAnalysis> analyses = new();
        foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
        {
            ReclaimAnalysis analysis = new(item);
            analyses.Add(analysis);
            TryAnalyze(analysis, inventory, player);
        }

        return analyses;
    }

    private static void ReclaimOne(ReclaimAnalysis analysis, Inventory inventory, Player player)
    {
        if (!TryAnalyze(analysis, inventory, player) || analysis.RecyclingImpediments.Count > 0)
            return;
        ApplyToInventory(analysis, inventory, player);
    }

    /// False when no recipe makes the item.
    internal static bool TryAnalyze(ReclaimAnalysis analysis, Inventory inventory, Player player)
    {
        if (!TryFindRecipe(analysis, player))
            return false;
        AnalyzeCraftingStation(analysis);
        AnalyzeYield(analysis);
        AnalyzeFreeSlots(analysis, inventory);
        AnalyzeDisplay(analysis, inventory, player);
        return true;
    }

    private static void AnalyzeCraftingStation(ReclaimAnalysis analysis)
    {
        if (RequireExactCraftingStationForRecycling.Value.IsOff())
            return;
        CraftingStation? required = analysis.Recipe!.m_craftingStation;
        if (required == null)
            return;
        CraftingStation? current = Player.m_localPlayer.GetCurrentCraftingStation();
        if (current == null || current.m_name != required.m_name || current.GetLevel() < analysis.Item.m_quality)
            analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_recipe_requires", ReclaimText.Localize(required.m_name), analysis.Item.m_quality.ToString()));
    }

    private static void AnalyzeDisplay(ReclaimAnalysis analysis, Inventory inventory, Player player)
    {
        if (player.GetInventory() != inventory)
            return;

        if (analysis.Item.m_equipped && HideEquippedItemsInRecyclingTab.Value.IsOn())
            analysis.DisplayImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_item_equipped"));

        if (analysis.Item.m_gridPos.y == 0 && IgnoreItemsOnHotbar.Value.IsOn())
            analysis.DisplayImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_item_on_hotbar"));

        CraftingStation? station = analysis.Recipe!.m_craftingStation;
        if (station?.m_name != null && StationFilterEnabled.Value.IsOn()
                                    && StationFilterList.Contains(global::Utils.GetPrefabName(station.transform.root.gameObject)))
            analysis.DisplayImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_item_from_station", ReclaimText.Localize(station.m_name)));
    }

    /// Adds the yield and removes the item. Rolls back the additions when the
    /// inventory refuses one, so nothing is lost.
    internal static void ApplyToInventory(ReclaimAnalysis analysis, Inventory inventory, Player player, bool recordUndo = false)
    {
        List<ReclaimYield> added = new();
        foreach (ReclaimYield entry in analysis.Entries)
        {
            if (entry is { Amount: 0, InitialRecipeHadZero: true })
                continue;

            ItemDrop.ItemData? addedItem = ApplyCraftedBy.Value.IsOn()
                ? inventory.AddItem(entry.Prefab.name, entry.Amount, entry.Quality, entry.Variant, player.GetPlayerID(), player.GetPlayerName(), false)
                : inventory.AddItem(entry.Prefab.name, entry.Amount, entry.Quality, entry.Variant, 0, "", false);

            if (addedItem != null)
            {
                added.Add(entry);
                continue;
            }

            if (entry.Amount < 1 && PreventZeroResourceYields.Value.IsOff())
                continue;

            StashLog.Error("The inventory refused a reclaimed resource after a valid analysis. Rolling back so nothing is lost.");
            foreach (ReclaimYield rollback in added)
                inventory.RemoveItem(rollback.RecipeItemData.m_shared.m_name, rollback.Amount);

            analysis.ShouldErrorDumpAnalysis = true;
            analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_inventory_couldnt_add", ReclaimText.Localize(entry.Prefab.name)));
            analysis.Dump();
            return;
        }

        if (inventory.RemoveItem(analysis.Item))
        {
            if (recordUndo)
                ReclaimUndo.Record(analysis.Item, analysis.Entries);
            return;
        }

        StashLog.Error("The inventory refused to remove a reclaimed item after a valid analysis.");
        analysis.ShouldErrorDumpAnalysis = true;
        analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_inventory_couldnt_remove", ReclaimText.Localize(analysis.Item.m_shared.m_name)));
        analysis.Dump();
    }

    /// With several recipes for one item, prefer the ones the player knows, then
    /// the one that crafts the fewest.
    private static bool TryFindRecipe(ReclaimAnalysis analysis, Player player)
    {
        ItemDrop.ItemData item = analysis.Item;
        List<Recipe> found = RecipeCache.TryGetValue(item.m_shared.m_name, out List<Recipe> cached)
            ? cached.Where(recipe => recipe?.m_item?.m_itemData?.m_shared != null).ToList()
            : new List<Recipe>();
        if (found.Count == 0)
        {
            analysis.DisplayImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_couldnt_find_recipe", ReclaimText.Localize(item.m_shared.m_name)));
            analysis.Recipe = null;
            return false;
        }

        if (found.Count > 1)
        {
            List<Recipe> known = found.Where(recipe => player.IsRecipeKnown(recipe.m_item.m_itemData.m_shared.m_name)).ToList();
            if (known.Count > 0)
                found = known;
            found = found.OrderBy(recipe => recipe.m_amount).Take(1).ToList();
        }

        analysis.Recipe = found[0];
        if (!player.IsRecipeKnown(analysis.Recipe.m_item.m_itemData.m_shared.m_name) && AllowRecyclingUnknownRecipes.Value.IsOff())
            analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_recipe_not_known", ReclaimText.Localize(item.m_shared.m_name)));

        return true;
    }

    /// Counts the slots the yield needs, filling partial stacks first, including
    /// stacks an earlier entry of the same yield already topped up.
    private static void AnalyzeFreeSlots(ReclaimAnalysis analysis, Inventory inventory)
    {
        Dictionary<(string, int), int> committed = new();
        int originalEmptySlots = inventory.GetEmptySlots();
        int remainingEmptySlots = originalEmptySlots;
        int totalNewSlotsNeeded = 0;

        foreach (ReclaimYield entry in analysis.Entries)
        {
            if (entry is { Amount: 0, InitialRecipeHadZero: true })
                continue;
            ItemDrop.ItemData item = entry.Prefab.GetComponent<ItemDrop>().m_itemData;
            (string, int) key = (item.m_shared.m_name, item.m_worldLevel);
            int freeStack = inventory.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel);
            committed.TryGetValue(key, out int used);
            int effectiveStack = Math.Max(0, freeStack - used);

            int overflow = Math.Max(0, entry.Amount - effectiveStack);
            if (overflow > 0)
            {
                int slotsNeeded = (int)Math.Ceiling(overflow / (double)item.m_shared.m_maxStackSize);
                totalNewSlotsNeeded += slotsNeeded;
                remainingEmptySlots -= slotsNeeded;
                committed[key] = used + effectiveStack;
            }
            else
            {
                committed[key] = used + entry.Amount;
            }

            if (remainingEmptySlots >= 0)
                continue;
            analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_not_enough_slots", totalNewSlotsNeeded.ToString(), originalEmptySlots.ToString()));
            return;
        }
    }

    private static void AnalyzeYield(ReclaimAnalysis analysis)
    {
        ItemDrop.ItemData item = analysis.Item;
        string prefabName = item.m_dropPrefab != null ? global::Utils.GetPrefabName(item.m_dropPrefab.name) : "";
        float recyclingRate = ReclaimRules.RecycleRateOverride(prefabName) ?? RecyclingRate.Value;
        Recipe recipe = analysis.Recipe!;
        double stackFraction = item.m_stack / (double)recipe.m_amount;

        foreach (Piece.Requirement resource in recipe.m_resources)
        {
            ItemDrop.ItemData? resourceData = resource.m_resItem?.m_itemData;
            if (resourceData?.m_shared == null)
                continue;

            GameObject? prefab = ReclaimItems.FindItemPrefab(resourceData);
            if (prefab == null)
            {
                StashLog.Warning($"Could not find a prefab for {item.m_shared.m_name}, so its resources cannot be spawned.");
                analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_coundnt_find_item", ReclaimText.Localize(item.m_shared.m_name), item.m_shared.m_name));
                continue;
            }

            int amountPerLevelSum = ReclaimMath.SpentQualityLevels(resource.m_upgraderResource, item.m_shared.m_maxQuality, item.m_quality)
                .Select(resource.GetAmount).Sum();
            (int amount, bool initialRecipeHadZero) = ReclaimMath.FinalAmount(amountPerLevelSum, stackFraction, recyclingRate,
                item.m_shared.m_maxStackSize == 1, UnstackableItemsAlwaysReturnAtLeastOneResource.Value.IsOn());
            if (DebugAllowSpammyLogs.Value.IsOn())
                StashLog.Debug($"Reclaim yield: {resourceData.m_shared.m_name} amount {resource.m_amount} levels {amountPerLevelSum} quality {item.m_quality} " +
                               $"stack {item.m_stack}/{item.m_shared.m_maxStackSize} fraction {stackFraction} rate {recyclingRate} final {amount}");

            if (!ReclaimRules.IsExcludedInReclaiming(global::Utils.GetPrefabName(prefab)))
                analysis.Entries.Add(new ReclaimYield(prefab, resourceData, amount, resourceData.m_quality, resourceData.m_variant, initialRecipeHadZero));

            if (PreventZeroResourceYields.Value.IsOn() && amount == 0 && !initialRecipeHadZero)
                analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_recylce_yield_none", ReclaimText.Localize(resourceData.m_shared.m_name)));
        }

        if (ReclaimReturnEnchantedResources.Value.IsOff())
            return;
        if (ReclaimFeature.HasEpicLoot && !AddEpicLootYield(analysis))
            return;
        if (Jewelcrafting.API.IsLoaded())
            AddGemYield(analysis);
    }

    /// False when an impediment was recorded and the analysis should stop.
    private static bool AddEpicLootYield(ReclaimAnalysis analysis)
    {
        ItemDrop.ItemData item = analysis.Item;
        if (!item.IsMagicItem() || !item.TryGetRarity(out var rarity))
            return true;

        foreach (var cost in item.GetEnchantCosts(rarity))
        {
            GameObject? costPrefab = ObjectDB.instance.GetItemPrefab(cost.Item);
            ItemDrop.ItemData? costData = costPrefab == null ? null : costPrefab.GetComponent<ItemDrop>()?.m_itemData;
            if (costData == null)
                continue;

            Recipe? recipe = ObjectDB.instance.GetRecipe(costData);
            if (!ReclaimItems.MaterialAllowed(costData, recipe, AllowRecyclingUnknownRecipes.Value.IsOn()))
            {
                analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_recipe_not_known", ReclaimText.Localize(costData.m_shared.m_name)));
                return false;
            }

            if (ReclaimRules.IsExcludedInReclaiming(global::Utils.GetPrefabName(costPrefab)))
                continue;

            // Magic materials usually have no recipe; fall back to the stack the prefab carries.
            analysis.Entries.Add(new ReclaimYield(costPrefab!, costData, recipe != null ? recipe.m_amount : costData.m_stack, costData.m_quality, costData.m_variant, false));
        }

        return true;
    }

    private static void AddGemYield(ReclaimAnalysis analysis)
    {
        var gems = Jewelcrafting.API.GetGems(analysis.Item);
        if (gems == null)
            return;

        foreach (var gem in gems)
        {
            if (gem == null)
                continue;
            ItemDrop? gemDrop = ObjectDB.instance?.GetItemPrefab(gem.gemPrefab)?.GetComponent<ItemDrop>();
            if (gemDrop == null)
                continue;

            ItemDrop.ItemData gemData = gemDrop.m_itemData;
            Recipe? recipe = ObjectDB.instance!.GetRecipe(gemData);
            if (!ReclaimItems.MaterialAllowed(gemData, recipe, AllowRecyclingUnknownRecipes.Value.IsOn()))
            {
                analysis.RecyclingImpediments.Add(ReclaimText.Localize("$ottostash_reclaim_recipe_not_known", ReclaimText.Localize(gemData.m_shared.m_name)));
                return;
            }

            if (ReclaimRules.IsExcludedInReclaiming(global::Utils.GetPrefabName(gemDrop.gameObject)))
                continue;

            // Merged gems have no recipe and come back as one gem.
            analysis.Entries.Add(new ReclaimYield(gemDrop.gameObject, gemData, recipe != null ? recipe.m_amount : 1, gemData.m_quality, gemData.m_variant, false));
        }
    }
}

/// Item and prefab lookups shared by reclaiming and discarding.
internal static class ReclaimItems
{
    internal static GameObject? FindItemPrefab(ItemDrop.ItemData itemData)
    {
        if (ObjectDB.instance.TryGetItemPrefab(itemData.m_shared, out GameObject byData) && byData != null)
            return byData;

        foreach (GameObject prefab in ObjectDB.instance.m_items)
        {
            if (prefab == null)
                continue;
            ItemDrop? drop = prefab.GetComponent<ItemDrop>();
            if (drop?.m_itemData?.m_shared != null && drop.m_itemData.m_shared.m_name == itemData.m_shared.m_name)
                return prefab;
        }

        return null;
    }

    /// Whether a material that comes back from an enchantment or a gem may be
    /// returned: the player must know the material, and its recipe if it has one.
    internal static bool MaterialAllowed(ItemDrop.ItemData material, Recipe? recipe, bool allowUnknown)
    {
        if (allowUnknown)
            return true;
        Player player = Player.m_localPlayer;
        bool knownMaterial = player.m_knownMaterial.Contains(material.m_shared.m_name);
        bool knownRecipe = player.IsRecipeKnown(material.m_shared.m_name);
        return knownMaterial && (recipe == null || knownRecipe);
    }
}

internal static class ReclaimText
{
    internal static string Localize(string text) => Localization.instance.Localize(text);

    internal static string Localize(string text, params string[] words) => Localization.instance.Localize(text, words);
}
