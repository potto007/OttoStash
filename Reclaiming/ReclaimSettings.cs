namespace OttoStash;

/// The settings for reclaiming, inventory discard and trash marking, carried
/// over from Recycle_N_Reclaim. Section and key names follow the PascalCase rule
/// the other Otto mods use; ReclaimCarryOver maps the old spellings onto them.
public partial class OttoStashPlugin
{
    // Reclaim
    internal static ConfigEntry<Toggle> ApplyCraftedBy = null!;
    internal static ConfigEntry<float> RecyclingRate = null!;
    internal static ConfigEntry<Toggle> UnstackableItemsAlwaysReturnAtLeastOneResource = null!;
    internal static ConfigEntry<Toggle> RequireExactCraftingStationForRecycling = null!;
    internal static ConfigEntry<Toggle> ReclaimReturnEnchantedResources = null!;
    internal static ConfigEntry<Toggle> PreventZeroResourceYields = null!;
    internal static ConfigEntry<Toggle> AllowRecyclingUnknownRecipes = null!;
    internal static ConfigEntry<bool> ShowRecycleYieldInTooltip = null!;
    internal static ConfigEntry<KeyboardShortcut> UndoRecycleKeybind = null!;
    internal static ConfigEntry<float> UndoRecycleGracePeriodSeconds = null!;

    // ReclaimUI
    internal static ConfigEntry<Toggle> ReclaimTabEnabled = null!;
    internal static ConfigEntry<Toggle> ContainerRecyclingEnabled = null!;
    internal static ConfigEntry<Vector3> ContainerButtonPosition = null!;
    internal static ConfigEntry<Toggle> NotifyOnSalvagingImpediments = null!;
    internal static ConfigEntry<Toggle> HideEquippedItemsInRecyclingTab = null!;
    internal static ConfigEntry<Toggle> IgnoreItemsOnHotbar = null!;
    internal static ConfigEntry<float> RecipeListScrollSpeedMultiplier = null!;
    internal static ConfigEntry<Toggle> StationFilterEnabled = null!;
    internal static ConfigEntry<string> StationFilterListString = null!;
    internal static List<string> StationFilterList = new();

    // Discard
    internal static ConfigEntry<Toggle> DiscardEnabled = null!;
    internal static ConfigEntry<Toggle> DiscardLockToAdmin = null!;
    internal static ConfigEntry<KeyboardShortcut> DiscardHotkey = null!;
    internal static ConfigEntry<Toggle> DiscardReturnUnknownResources = null!;
    internal static ConfigEntry<Toggle> DiscardReturnEnchantedResources = null!;
    internal static ConfigEntry<float> DiscardReturnResources = null!;

    // Trash
    internal static ConfigEntry<KeyboardShortcut> TrashingModifierKeybind = null!;
    internal static ConfigEntry<KeyboardShortcut> TrashingKeybind = null!;
    internal static ConfigEntry<Color> BorderColorTrashedSlot = null!;
    internal static ConfigEntry<bool> TrashTooltipHint = null!;
    internal static ConfigEntry<string> TrashedSlotTooltip = null!;

    // ReclaimDebug
    internal static ConfigEntry<Toggle> DebugAlwaysDumpAnalysisContext = null!;
    internal static ConfigEntry<Toggle> DebugAllowSpammyLogs = null!;

    /// True when the local player may use inventory discard and bulk trashing.
    internal static bool DiscardAllowed => DiscardEnabled.Value.IsOn() && (DiscardLockToAdmin.Value.IsOff() || ConfigSync.IsAdmin);

    private void BindReclaimSettings()
    {
        ApplyCraftedBy = config("Reclaim", "ApplyCraftedBy", Toggle.On,
            "If on, you are the crafter of every item that reclaiming or discarding gives back. If off, the crafter is left empty.");
        RecyclingRate = config("Reclaim", "RecyclingRate", 0.5f, new ConfigDescription(
            "The share of an item's crafting resources that reclaiming returns, from 0 to 1. The amount always rounds down, so 2.5 becomes 2. " +
            "At 0.5 a quality 1 item returns half of what it cost. Higher quality items return more, because their upgrades cost more. " +
            "The recycleRates list in potto007.OttoStash.Reclaim.yml overrides this for single items or groups.",
            new AcceptableValueRange<float>(0f, 1f)));
        UnstackableItemsAlwaysReturnAtLeastOneResource = config("Reclaim", "UnstackableItemsAlwaysReturnAtLeastOneResource", Toggle.On,
            "If on, reclaiming an item that does not stack returns at least 1 of each resource that would otherwise round down to 0.");
        RequireExactCraftingStationForRecycling = config("Reclaim", "RequireExactCraftingStationForRecycling", Toggle.On,
            "If on, reclaiming needs the crafting station the item is made at, at the level its quality needs. If off, any station will do.");
        ReclaimReturnEnchantedResources = config("Reclaim", "ReturnEnchantedResources", Toggle.On,
            "If on and Epic Loot or Jewelcrafting is installed, reclaiming also returns the enchanting materials or socketed gems.");
        PreventZeroResourceYields = config("Reclaim", "PreventZeroResourceYields", Toggle.On,
            "If on, an item that would return 0 of any resource cannot be reclaimed, so nothing is lost to rounding.");
        AllowRecyclingUnknownRecipes = config("Reclaim", "AllowRecyclingUnknownRecipes", Toggle.Off,
            "If on, you can reclaim items whose recipe you have not learned yet. Off by default, because it skips progression.");
        ShowRecycleYieldInTooltip = config("Reclaim", nameof(ShowRecycleYieldInTooltip), false,
            "If on, an item's tooltip lists what reclaiming it would return.", false);
        UndoRecycleKeybind = config("Reclaim", nameof(UndoRecycleKeybind), new KeyboardShortcut(KeyCode.Z, KeyCode.LeftControl),
            new ConfigDescription("Undoes the last reclaim from the Reclaim tab, within the grace period.", new AcceptableShortcuts()), false);
        UndoRecycleGracePeriodSeconds = config("Reclaim", nameof(UndoRecycleGracePeriodSeconds), 20f, new ConfigDescription(
            "Seconds after a reclaim during which it can be undone. 0 turns undo off.", new AcceptableValueRange<float>(0f, 3600f)));

        ReclaimTabEnabled = config("ReclaimUI", "ReclaimTabEnabled", Toggle.On,
            "If on, crafting stations get a Reclaim tab next to Craft and Upgrade.");
        ContainerRecyclingEnabled = config("ReclaimUI", "ContainerRecyclingEnabled", Toggle.On,
            "If on, an open container shows a Reclaim All button. Click it twice to reclaim everything in the container.");
        ContainerButtonPosition = config("ReclaimUI", "ContainerButtonPosition", new Vector3(496.0f, -374.0f, -1.0f),
            "Where the Reclaim All button sits. Hold Left Ctrl and drag the button with the right mouse button to move it; the new position is saved here.", false);
        NotifyOnSalvagingImpediments = config("ReclaimUI", "NotifyOnSalvagingImpediments", Toggle.On,
            "If on, Reclaim All shows a message in the middle of the screen for every item it could not reclaim, and why: " +
            "not enough free slots, an unknown recipe, or a resource that would return 0.");
        HideEquippedItemsInRecyclingTab = config("ReclaimUI", "HideRecipesForEquippedItems", Toggle.On,
            "If on, items you have equipped are left out of the Reclaim tab.");
        IgnoreItemsOnHotbar = config("ReclaimUI", "IgnoreItemsOnHotbar", Toggle.On,
            "If on, items on your hotbar are left out of the Reclaim tab.");
        RecipeListScrollSpeedMultiplier = config("ReclaimUI", "RecipeListScrollSpeedMultiplier", 10f, new ConfigDescription(
            "Multiplies how far the mouse wheel scrolls the crafting and Reclaim recipe lists. Newer Unity reports much smaller wheel steps, " +
            "which made the vanilla speed crawl. 1 is vanilla speed. Raise it toward 20 if it still crawls, as it can on Linux and Proton.",
            new AcceptableValueRange<float>(0.1f, 50f)), false);
        RecipeListScrollSpeedMultiplier.SettingChanged += (_, _) => Reclaiming.RecipeListScroll.Apply();
        StationFilterEnabled = config("ReclaimUI", "StationFilterEnabled", Toggle.On,
            "If on, items made at the stations in StationFilterList are left out of the Reclaim tab. It keeps food out of the list.");
        StationFilterListString = config("ReclaimUI", "StationFilterList", "piece_cauldron",
            "Comma separated prefab names of the crafting stations whose items the Reclaim tab leaves out. The vanilla stations are " +
            "forge, blackforge, piece_workbench, piece_cauldron, piece_stonecutter, piece_artisanstation and piece_magetable.");
        StationFilterListString.SettingChanged += (_, _) => SplitStationFilterList();
        SplitStationFilterList();

        DiscardEnabled = config("Discard", "Enabled", Toggle.On,
            "If on, pressing DiscardHotkey while dragging an item in your inventory destroys it and returns its resources.");
        DiscardLockToAdmin = config("Discard", "LockToAdmin", Toggle.On,
            "If on, only admins can discard from the inventory or bulk trash marked slots.");
        DiscardHotkey = config("Discard", "DiscardHotkey", new KeyboardShortcut(KeyCode.Delete),
            new ConfigDescription("Press while dragging an item to discard it.", new AcceptableShortcuts()), false);
        DiscardReturnUnknownResources = config("Discard", "ReturnUnknownResources", Toggle.Off,
            "If on, discarding returns resources even when you do not know the item's recipe.");
        DiscardReturnEnchantedResources = config("Discard", "ReturnEnchantedResources", Toggle.On,
            "If on and Epic Loot or Jewelcrafting is installed, discarding also returns the enchanting materials or socketed gems.");
        DiscardReturnResources = config("Discard", "ReturnResources", 1f, new ConfigDescription(
            "The share of an item's resources that discarding returns, from 0 to 1. 0 destroys the item and returns nothing.",
            new AcceptableValueRange<float>(0f, 1f)));

        TrashingModifierKeybind = config("Trash", "TrashingModifierKeybind", new KeyboardShortcut(KeyCode.X),
            new ConfigDescription("While holding this, left clicking an inventory slot marks or unmarks it as trash. Letting go clears every mark.", new AcceptableShortcuts()), false);
        TrashingKeybind = config("Trash", "TrashingKeybind", new KeyboardShortcut(KeyCode.Mouse2),
            new ConfigDescription("Pressed while holding TrashingModifierKeybind, discards every marked slot. The default is the middle mouse button.", new AcceptableShortcuts()), false);
        BorderColorTrashedSlot = config("Trash", "BorderColorTrashedSlot", new Color(1f, 0f, 0f),
            "Color of the border on a slot marked as trash.", false);
        TrashTooltipHint = config("Trash", "DisplayTooltipHint", true,
            "If on, a marked slot's tooltip says it is marked as trash.", false);
        TrashedSlotTooltip = config("Trash", "TrashedSlotTooltip", "Slot is marked as trash and will be discarded",
            "The tooltip line on a slot marked as trash.", false);

        DebugAlwaysDumpAnalysisContext = config("ReclaimDebug", "DebugAlwaysDumpAnalysisContext", Toggle.Off,
            "If on, every reclaim writes a full report to the log. Slow; turn it on only to chase a problem.");
        DebugAllowSpammyLogs = config("ReclaimDebug", "DebugAllowSpammyLogs", Toggle.Off,
            "If on, every yield calculation is written to the log. Very noisy and slow.");
    }

    private static void SplitStationFilterList()
    {
        StationFilterList = StationFilterListString.Value.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();
    }
}
