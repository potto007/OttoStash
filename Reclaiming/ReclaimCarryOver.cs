namespace OttoStash.Reclaiming;

/// Carries a Recycle_N_Reclaim install over on the first run that has the
/// reclaim settings: its .cfg values into the new sections of the OttoStash
/// .cfg, and its exclude lists into potto007.OttoStash.Reclaim.yml. Never
/// touches the old files, and never overwrites reclaim settings OttoStash
/// already has.
internal static class ReclaimCarryOver
{
    internal const string OldGuid = "Azumatt.Recycle_N_Reclaim";
    internal const string OldConfigFileName = OldGuid + ".cfg";
    internal const string OldYamlFileName = OldGuid + "_ExcludeLists.yml";

    /// The first reclaim section BindReclaimSettings writes. Its presence in the
    /// OttoStash .cfg means the reclaim settings were bound on an earlier run.
    internal const string MarkerSection = "Reclaim";

    /// Old (section, key) to new (section, key). "1 - General / Lock Configuration"
    /// is left out on purpose: OttoStash has its own lock.
    internal static readonly Dictionary<(string Section, string Key), (string Section, string Key)> Renames = new()
    {
        [("1 - General", "Apply Crafted By")] = ("Reclaim", "ApplyCraftedBy"),

        [("2 - Inventory Recycle", "Enabled")] = ("Discard", "Enabled"),
        [("2 - Inventory Recycle", "Lock to Admin")] = ("Discard", "LockToAdmin"),
        [("2 - Inventory Recycle", "DiscardHotkey(s)")] = ("Discard", "DiscardHotkey"),
        [("2 - Inventory Recycle", "ReturnUnknownResources")] = ("Discard", "ReturnUnknownResources"),
        [("2 - Inventory Recycle", "ReturnEnchantedResources")] = ("Discard", "ReturnEnchantedResources"),
        [("2 - Inventory Recycle", "ReturnResources")] = ("Discard", "ReturnResources"),
        [("2 - Inventory Recycle", "BorderColorTrashedItem")] = ("Trash", "BorderColorTrashedSlot"),
        [("2 - Inventory Recycle", "DisplayTooltipHint")] = ("Trash", "DisplayTooltipHint"),
        [("2 - Inventory Recycle", "ShowRecycleYieldInTooltip")] = ("Reclaim", "ShowRecycleYieldInTooltip"),
        [("2 - Inventory Recycle", "TrashingKeybind")] = ("Trash", "TrashingKeybind"),
        [("2 - Inventory Recycle", "TrashingModifierKeybind1")] = ("Trash", "TrashingModifierKeybind"),
        [("2 - Inventory Recycle", "TrashedSlotTooltip")] = ("Trash", "TrashedSlotTooltip"),

        [("3 - Reclaiming", "RecyclingRate")] = ("Reclaim", "RecyclingRate"),
        [("3 - Reclaiming", "UnstackableItemsAlwaysReturnAtLeastOneResource")] = ("Reclaim", "UnstackableItemsAlwaysReturnAtLeastOneResource"),
        [("3 - Reclaiming", "RequireExactCraftingStationForRecycling")] = ("Reclaim", "RequireExactCraftingStationForRecycling"),
        [("3 - Reclaiming", "ReturnEnchantedResources")] = ("Reclaim", "ReturnEnchantedResources"),
        [("3 - Reclaiming", "PreventZeroResourceYields")] = ("Reclaim", "PreventZeroResourceYields"),
        [("3 - Reclaiming", "AllowRecyclingUnknownRecipes")] = ("Reclaim", "AllowRecyclingUnknownRecipes"),
        [("3 - Reclaiming", "UndoRecycleKeybind")] = ("Reclaim", "UndoRecycleKeybind"),
        [("3 - Reclaiming", "UndoRecycleGracePeriodSeconds")] = ("Reclaim", "UndoRecycleGracePeriodSeconds"),

        [("4 - UI", "ContainerButtonPosition")] = ("ReclaimUI", "ContainerButtonPosition"),
        [("4 - UI", "ContainerRecyclingEnabled")] = ("ReclaimUI", "ContainerRecyclingEnabled"),
        [("4 - UI", "NotifyOnSalvagingImpediments")] = ("ReclaimUI", "NotifyOnSalvagingImpediments"),
        [("4 - UI", "EnableExperimentalCraftingTabUI")] = ("ReclaimUI", "ReclaimTabEnabled"),
        [("4 - UI", "HideRecipesForEquippedItems")] = ("ReclaimUI", "HideRecipesForEquippedItems"),
        [("4 - UI", "IgnoreItemsOnHotbar")] = ("ReclaimUI", "IgnoreItemsOnHotbar"),
        [("4 - UI", "RecipeListScrollSpeedMultiplier")] = ("ReclaimUI", "RecipeListScrollSpeedMultiplier"),
        [("4 - UI", "StationFilterEnabled")] = ("ReclaimUI", "StationFilterEnabled"),
        [("4 - UI", "StationFilterList")] = ("ReclaimUI", "StationFilterList"),

        [("zDebug", "DebugAlwaysDumpAnalysisContext")] = ("ReclaimDebug", "DebugAlwaysDumpAnalysisContext"),
        [("zDebug", "DebugAllowSpammyLogs")] = ("ReclaimDebug", "DebugAllowSpammyLogs"),
    };

    /// Every "Key = Value" line of a BepInEx .cfg, by section and key.
    internal static Dictionary<(string Section, string Key), string> ParseCfg(string text)
    {
        Dictionary<(string, string), string> values = new();
        string section = "";
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line.Substring(1, line.Length - 2).Trim();
                continue;
            }

            int equals = line.IndexOf('=');
            if (equals <= 0)
                continue;

            values[(section, line.Substring(0, equals).Trim())] = line.Substring(equals + 1).Trim();
        }

        return values;
    }

    /// The old file's values under their new section and key, for the settings
    /// that have a new home.
    internal static Dictionary<(string Section, string Key), string> Map(string oldCfgText)
    {
        Dictionary<(string, string), string> mapped = new();
        foreach (KeyValuePair<(string Section, string Key), string> entry in ParseCfg(oldCfgText))
        {
            if (Renames.TryGetValue(entry.Key, out (string Section, string Key) target))
                mapped[target] = entry.Value;
        }

        return mapped;
    }

    /// True when the OttoStash .cfg text already holds the reclaim settings.
    internal static bool HasReclaimSettings(string cfgText)
    {
        return cfgText.Split('\n').Any(line => line.Trim().TrimStart('﻿') == "[" + MarkerSection + "]");
    }

    /// Read before any reclaim setting is bound: whether this run is the first
    /// one with reclaim settings, so their values may still be carried over.
    internal static bool IsFirstReclaimRun(string configPath)
    {
        try
        {
            return !File.Exists(configPath) || !HasReclaimSettings(File.ReadAllText(configPath));
        }
        catch (Exception e)
        {
            StashLog.Warning($"Could not read {configPath} to check for reclaim settings: {e.Message}");
            return false;
        }
    }

    /// Copies the old values onto the freshly bound entries.
    internal static void CarryOverSettings(ConfigFile config)
    {
        try
        {
            string oldConfig = Path.Combine(Paths.ConfigPath, OldConfigFileName);
            if (!File.Exists(oldConfig))
                return;

            int carried = 0;
            foreach (KeyValuePair<(string Section, string Key), string> entry in Map(File.ReadAllText(oldConfig)))
            {
                ConfigDefinition definition = new(entry.Key.Section, entry.Key.Key);
                if (!config.ContainsKey(definition))
                    continue;
                config[definition].SetSerializedValue(entry.Value);
                carried++;
            }

            StashLog.Info($"Carried {carried} Recycle_N_Reclaim settings over to the reclaim sections of {ModGUID}.cfg.");
        }
        catch (Exception e)
        {
            // A failure here must not stop the mod from loading.
            StashLog.Warning($"Could not carry the Recycle_N_Reclaim settings over: {e.Message}");
        }
    }

    /// Copies the old exclude lists when OttoStash has no reclaim rules file yet.
    internal static void CarryOverRules(string newYamlPath)
    {
        try
        {
            string oldYaml = Path.Combine(Paths.ConfigPath, OldYamlFileName);
            if (!File.Exists(oldYaml) || File.Exists(newYamlPath))
                return;
            File.Copy(oldYaml, newYamlPath);
            StashLog.Info($"Carried your Recycle_N_Reclaim exclude lists over to {Path.GetFileName(newYamlPath)}.");
        }
        catch (Exception e)
        {
            StashLog.Warning($"Could not carry the Recycle_N_Reclaim exclude lists over: {e.Message}");
        }
    }
}
