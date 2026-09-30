namespace OttoStash.Patches;

/// Keeps the autostore target list in step with the containers the game loads.
[HarmonyPatch]
internal static class ContainerRegistrationPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    private static void ContainerAwakePostfix(Container __instance)
    {
        Functions.LogContainerStatus(__instance);

        ZNetView? view = __instance.m_nview;
        if (view == null || view.GetZDO() == null)
            return;

        StorePause.RegisterHandler(view, __instance);
        StoreTargets.Register(__instance);
    }

    // A container the player opens is registered again in case its Awake ran
    // before the player had access to it.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    private static void ContainerInteractPostfix(Container __instance)
    {
        long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
        if ((__instance.m_checkGuardStone && !PrivateArea.CheckAccess(__instance.transform.position)) || !__instance.CheckAccess(playerId))
            return;

        StoreTargets.Register(__instance);
    }

    // Vanilla calls OnSpawned for the local player only, right after it becomes
    // the local player, so every chest that loaded ahead of them can be picked up here.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    private static void PlayerOnSpawnedPostfix(Player __instance)
    {
        if (__instance != Player.m_localPlayer)
            return;

        int added = StoreTargets.RegisterLoaded();
        Functions.LogDebug($"Spawn scan registered {added} loaded containers for autostore.");
    }
}
