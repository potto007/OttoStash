namespace OttoStash.Patches;

/// Lets crafting and building count and spend the materials in nearby
/// containers. Every check runs after vanilla and only when vanilla said no,
/// so a player who carries enough sees no change.
[HarmonyPatch]
internal static class PullPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.SetCraftingStation))]
    private static void SetCraftingStationPostfix(Player __instance, CraftingStation station)
    {
        if (__instance == Player.m_localPlayer)
            Pull.Station = station != null ? Utils.GetPrefabName(station.gameObject) : "";
    }

    // Vanilla HaveRequirements(Recipe) reaches its item check through this
    // method, so patching it covers the crafting menu and DoCrafting alike.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems))]
    private static void HaveRequirementItemsPostfix(Player __instance, Recipe piece, bool discover, int qualityLevel, int amount, ref bool __result)
    {
        if (__result || discover || piece == null || !Pull.Active(__instance))
            return;

        __result = HaveRecipeItems(__instance, piece, qualityLevel, amount);
    }

    /// Vanilla's rule with the nearby stock added: each requirement must be met
    /// at a single quality, or, for a recipe that takes one ingredient of a
    /// choice, any one of them must be.
    private static bool HaveRecipeItems(Player player, Recipe recipe, int qualityLevel, int amount)
    {
        CraftingStation? station = player.GetCurrentCraftingStation();
        foreach (Piece.Requirement requirement in recipe.m_resources)
        {
            if (!Pull.Applies(requirement, station))
                continue;

            int need = requirement.GetAmount(qualityLevel) * amount;
            bool met = BestAtOneQuality(player, requirement, need) >= need;
            if (recipe.m_requireOnlyOneIngredient)
            {
                if (met)
                    return true;
            }
            else if (!met)
            {
                return false;
            }
        }

        return !recipe.m_requireOnlyOneIngredient;
    }

    private static int BestAtOneQuality(Player player, Piece.Requirement requirement, int need)
    {
        ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
        string prefab = Pull.PrefabOf(requirement);
        int best = 0;
        for (int quality = 1; quality <= shared.m_maxQuality; quality++)
        {
            int count = player.GetInventory().CountItems(shared.m_name, quality) + Pull.Available(player, shared.m_name, prefab, quality);
            best = Math.Max(best, count);
            if (best >= need)
                break;
        }

        return best;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    private static void HaveRequirementsPiecePostfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
    {
        if (__result || piece == null || mode == Player.RequirementMode.IsKnown || !Pull.Active(__instance))
            return;
        if (!StationAndDlcAllow(__instance, piece, mode))
            return;

        Inventory inventory = __instance.GetInventory();
        foreach (Piece.Requirement requirement in piece.m_resources)
        {
            if (requirement.m_resItem == null || requirement.m_amount <= 0 || requirement.m_resItem.m_itemData?.m_shared == null)
                continue;

            string name = requirement.m_resItem.m_itemData.m_shared.m_name;
            int carried = inventory.CountItems(name);
            int need = mode == Player.RequirementMode.CanAlmostBuild ? 1 : requirement.m_amount;
            if (carried >= need)
                continue;
            if (carried + Pull.Available(__instance, name, Pull.PrefabOf(requirement), -1) < need)
                return;
        }

        __result = true;
    }

    /// The station and DLC gates of vanilla HaveRequirements(Piece), which the
    /// nearby stock must not get around.
    private static bool StationAndDlcAllow(Player player, Piece piece, Player.RequirementMode mode)
    {
        if (piece.m_craftingStation)
        {
            if (mode == Player.RequirementMode.CanAlmostBuild)
            {
                if (!player.m_knownStations.ContainsKey(piece.m_craftingStation.m_name))
                    return false;
            }
            else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, player.transform.position) && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench))
            {
                return false;
            }
        }

        return piece.m_dlc.Length <= 0 || DLCMan.instance.IsDLCInstalled(piece.m_dlc);
    }

    // The containers cover only the shortfall. Vanilla then removes what the
    // player carries, as it always does.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    private static void ConsumeResourcesPrefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier)
    {
        if (!Pull.Active(__instance))
            return;

        try
        {
            CraftingStation? station = __instance.GetCurrentCraftingStation();
            Inventory inventory = __instance.GetInventory();
            foreach (Piece.Requirement requirement in requirements)
            {
                if (!Pull.Applies(requirement, station))
                    continue;

                int need = requirement.GetAmount(qualityLevel) * multiplier;
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int shortfall = need - inventory.CountItems(name, itemQuality);
                if (shortfall > 0)
                    Pull.Take(__instance, name, Pull.PrefabOf(requirement), itemQuality, shortfall);
            }
        }
        catch (Exception e)
        {
            StashLog.Error($"Could not pull crafting materials from nearby containers: {e}");
        }
    }

    // A recipe that takes one ingredient of a choice asks for the first one the
    // player can pay. When nothing carried is enough, offer one the containers
    // can make up. Only its name and quality are read afterwards.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
    private static void GetFirstRequiredItemPostfix(Player __instance, Inventory inventory, Recipe recipe, int qualityLevel, ref int amount, ref int extraAmount, int craftMultiplier, ref ItemDrop.ItemData? __result)
    {
        if (__result != null || recipe == null || !Pull.Active(__instance))
            return;

        CraftingStation? station = __instance.GetCurrentCraftingStation();
        foreach (Piece.Requirement requirement in recipe.m_resources)
        {
            if (!Pull.Applies(requirement, station))
                continue;

            ItemDrop.ItemData.SharedData shared = requirement.m_resItem.m_itemData.m_shared;
            int need = requirement.GetAmount(qualityLevel) * craftMultiplier;
            for (int quality = 1; quality <= shared.m_maxQuality; quality++)
            {
                int have = __instance.GetInventory().CountItems(shared.m_name, quality) + Pull.Available(__instance, shared.m_name, Pull.PrefabOf(requirement), quality);
                if (have < need)
                    continue;

                ItemDrop.ItemData? item = inventory.GetItem(shared.m_name, quality);
                if (item == null)
                {
                    item = requirement.m_resItem.m_itemData.Clone();
                    item.m_quality = quality;
                    item.m_dropPrefab = requirement.m_resItem.gameObject;
                }

                __result = item;
                amount = need;
                extraAmount = requirement.m_extraAmountOnlyOneIngredient;
                return;
            }
        }
    }

    // DoCrafting pays a one-ingredient recipe with a single RemoveItem on the
    // player's inventory, after every check has passed. That call is where the
    // containers make up what the player lacks.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    private static void DoCraftingPrefix(InventoryGui __instance)
    {
        Recipe? recipe = __instance.m_craftRecipe;
        SingleIngredientCraft.Recipe = recipe != null && recipe.m_requireOnlyOneIngredient ? recipe : null;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    private static void DoCraftingFinalizer()
    {
        SingleIngredientCraft.Recipe = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(string), typeof(int), typeof(int), typeof(bool))]
    private static void InventoryRemoveItemPrefix(Inventory __instance, string name, int amount, int itemQuality)
    {
        Recipe? recipe = SingleIngredientCraft.Recipe;
        if (recipe == null)
            return;

        Player player = Player.m_localPlayer;
        if (player == null || __instance != player.GetInventory() || !Pull.Active(player))
            return;

        Piece.Requirement? requirement = recipe.m_resources.FirstOrDefault(r => r.m_resItem != null && r.m_resItem.m_itemData.m_shared.m_name == name);
        if (requirement == null)
            return;

        // One payment per craft.
        SingleIngredientCraft.Recipe = null;

        int shortfall = amount - __instance.CountItems(name, itemQuality);
        if (shortfall > 0)
            Pull.Take(player, name, Pull.PrefabOf(requirement), itemQuality, shortfall);
    }

    private static class SingleIngredientCraft
    {
        internal static Recipe? Recipe;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.VeryHigh)]
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static void ObjectDBAwakePostfix(ObjectDB __instance)
    {
        PullStatusEffect.Register(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    private static void PlayerOnSpawnedPostfix(Player __instance)
    {
        if (__instance == Player.m_localPlayer)
            PullSwitch.ApplyStatusEffect(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnRespawn))]
    private static void PlayerOnRespawnPostfix(Player __instance)
    {
        if (__instance == Player.m_localPlayer)
            PullSwitch.ApplyStatusEffect(__instance);
    }
}
