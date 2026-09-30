namespace OttoStash.Patches;

/// One-time setup once the game's data and world exist: the ground pickup
/// ticker, the predefined item groups, and the console command.
[HarmonyPatch]
internal static class StartupPatches
{
    private const string PickupObjectName = "OttoStash_GroundPickup";

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Game), nameof(Game.Start))]
    private static void GameStartPostfix()
    {
        GroundPickup pickup = new GameObject(PickupObjectName).AddComponent<GroundPickup>();
        UnityEngine.Object.DontDestroyOnLoad(pickup);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    private static void ObjectDBAwakePostfix(ObjectDB __instance)
    {
        if (ZNetScene.instance == null)
            return;
        PredefinedGroups.Build(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    private static void TerminalInitTerminalPostfix()
    {
        ContainerSearch.Register();
    }
}
