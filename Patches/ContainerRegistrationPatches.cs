namespace OttoStash.Patches;

/// Keeps the autostore target list in step with the containers the game loads
/// and destroys.
[HarmonyPatch]
internal static class ContainerRegistrationPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    private static void ContainerAwakePostfix(Container __instance)
    {
        LogStatus(__instance);

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
        StashLog.Debug($"Spawn scan registered {added} loaded containers for autostore.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
    private static void ContainerOnDestroyedPostfix(Container __instance)
    {
        ContainerRegistry.Remove(__instance);
    }

    // A piece being torn down takes the containers on it and under it with it.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnDestroy))]
    private static void WearNTearOnDestroyPrefix(WearNTear __instance)
    {
        foreach (Container container in __instance.GetComponentsInChildren<Container>())
            ContainerRegistry.Remove(container);
        foreach (Container container in __instance.GetComponentsInParent<Container>())
            ContainerRegistry.Remove(container);
    }

    // A teleport unloads the area behind the player, so dead entries are dropped
    // while it runs.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
    private static void PlayerUpdateTeleportPrefix()
    {
        if (Player.m_localPlayer == null || !Player.m_localPlayer.m_teleporting)
            return;
        ContainerRegistry.Prune();
    }

    private static void LogStatus(Container container)
    {
        try
        {
            StashLog.BuildDebug($"Container {container.name} at {container.transform.position} is {(container.m_nview.IsOwner() ? "owned" : "not owned")} and has {(container.GetInventory()?.NrOfItems() ?? 0)} items.");
        }
        catch
        {
            // Diagnostics only.
        }
    }
}
