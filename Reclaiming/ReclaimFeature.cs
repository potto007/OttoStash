namespace OttoStash.Reclaiming;

/// Switches reclaiming and trash marking on or off as a whole. When
/// Recycle_N_Reclaim itself is installed, OttoStash leaves both features to it:
/// none of the patches in the Reclaiming and Trashing namespaces are applied and
/// no reclaim UI is created, while storing works as before.
internal static class ReclaimFeature
{
    private const string ReclaimingNamespace = "OttoStash.Reclaiming";
    private const string TrashingNamespace = "OttoStash.Trashing";

    internal static bool YieldsToRecycleNReclaim { get; private set; }
    internal static bool HasEpicLoot { get; private set; }
    internal static ReclaimTab? Tab { get; private set; }

    internal static void DetectRecycleNReclaim()
    {
        YieldsToRecycleNReclaim = Chainloader.PluginInfos.ContainsKey(ReclaimCarryOver.OldGuid);
        if (YieldsToRecycleNReclaim)
            StashLog.Warning("Recycle_N_Reclaim is installed, so OttoStash leaves reclaiming and trash marking to it. Its settings and exclude lists are copied into OttoStash on every start; remove Recycle_N_Reclaim to switch to the OttoStash versions.");
    }

    /// Whether a type's Harmony patches belong to the reclaim features.
    internal static bool Owns(Type type)
    {
        string? ns = type.Namespace;
        return ns != null && (ns == ReclaimingNamespace || ns.StartsWith(ReclaimingNamespace + ".")
                                                         || ns == TrashingNamespace || ns.StartsWith(TrashingNamespace + "."));
    }

    internal static void Start(GameObject host)
    {
        if (YieldsToRecycleNReclaim)
            return;

        HasEpicLoot = EpicLootAPI.EpicLoot.IsLoaded();
        if (HasEpicLoot)
            StashLog.Debug($"Epic Loot {EpicLootAPI.EpicLoot.GetPluginVersion()} found, reclaiming returns enchanting materials");

        host.AddComponent<ContainerReclaimButton>();
        Tab = host.AddComponent<ReclaimTab>();
    }
}

[HarmonyPatch]
internal static class ReclaimStartupPatches
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static void ObjectDBAwakePostfix(ObjectDB __instance)
    {
        if (ZNetScene.instance == null)
            return;
        ReclaimGroups.Build(__instance);
        Reclaimer.BuildRecipeCache(__instance);
    }
}
