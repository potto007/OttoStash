namespace OttoStash.Reclaiming;

/// Hooks the Reclaim tab into the crafting panel: it takes over the panel while
/// selected, hides away from crafting stations, and follows inventory changes.
[HarmonyPatch]
internal static class ReclaimTabPatches
{
    [HarmonyPrefix]
    [HarmonyPriority(600)]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTabCraftPressed))]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTabUpgradePressed))]
    private static void InventoryGuiOnTabPressedPrefix(InventoryGui __instance)
    {
        if (ReclaimFeature.Tab == null)
            return;
        ReclaimFeature.Tab.SetInteractable(true);
        // Epic Loot needs the panel rebuilt when leaving the Reclaim tab.
        __instance.UpdateCraftingPanel();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateCraftingPanel))]
    private static bool InventoryGuiUpdateCraftingPanelPrefix()
    {
        return ReclaimFeature.Tab == null || !ReclaimFeature.Tab.InReclaimTab();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateCraftingPanel))]
    private static void InventoryGuiUpdateCraftingPanelPostfix()
    {
        ReclaimTab? tab = ReclaimFeature.Tab;
        if (tab == null || tab.InReclaimTab())
            return;

        Player player = Player.m_localPlayer;
        tab.SetInteractable(true);
        tab.SetActive(player.GetCurrentCraftingStation() || player.NoCostCheat());
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static void InventoryGuiHidePrefix()
    {
        ReclaimTab? tab = ReclaimFeature.Tab;
        if (tab == null || !tab.InReclaimTab())
            return;
        InventoryGui.instance.OnTabCraftPressed();
        tab.SetActive(false);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe), typeof(Player), typeof(float))]
    private static bool InventoryGuiUpdateRecipePrefix(Player player, float dt)
    {
        ReclaimTab? tab = ReclaimFeature.Tab;
        if (tab == null || !tab.InReclaimTab())
            return true;
        tab.UpdateRecipe(player, dt);
        return false;
    }

    /// Rebuilds the list only when the contents changed, not when items moved.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Changed))]
    private static void InventoryChangedPostfix(Inventory __instance)
    {
        ReclaimTab? tab = ReclaimFeature.Tab;
        if (tab == null || !tab.InReclaimTab() || Player.m_localPlayer == null || __instance != Player.m_localPlayer.GetInventory())
            return;
        if (!tab.HasInventoryChanged(__instance))
            return;
        tab.UpdateRecyclingList();
        InventoryGui.instance.SetRecipe(-1, false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem), typeof(ItemDrop.ItemData), typeof(bool))]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem), typeof(ItemDrop.ItemData), typeof(bool))]
    private static void HumanoidEquipPostfix(Humanoid __instance)
    {
        ReclaimTab? tab = ReclaimFeature.Tab;
        if (tab == null || __instance != Player.m_localPlayer || !tab.InReclaimTab())
            return;
        tab.UpdateRecyclingList();
        InventoryGui.instance.SetRecipe(-1, false);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
    private static void InventoryGuiUpdatePostfix()
    {
        if (!InventoryGui.IsVisible() || Player.m_localPlayer == null)
            return;
        if (UndoRecycleKeybind.Value.IsKeyDown() && ReclaimUndo.CanUndo)
            ReclaimUndo.TryUndo();
    }
}
