using System.Reflection.Emit;

namespace OttoStash.Patches;

[HarmonyPatch(typeof(Container), nameof(Container.OnDestroyed))]
internal static class ContainerOnDestroyedPatch
{
    private static void Postfix(Container __instance)
    {
        if (__instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L || __instance.GetInventory() == null || !__instance.m_nview.IsValid())
            return;
        Boxes.RemoveContainer(__instance);
    }
}

[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnDestroy))]
static class WearNTearOnDestroyPatch
{
    static void Prefix(WearNTear __instance)
    {
        Container[]? container = __instance.GetComponentsInChildren<Container>();
        Container[]? parentContainer = __instance.GetComponentsInParent<Container>();
        if (container.Length > 0)
        {
            foreach (Container c in container)
            {
                Boxes.RemoveContainer(c);
            }
        }

        if (parentContainer.Length <= 0) return;
        {
            foreach (Container c in parentContainer)
            {
                Boxes.RemoveContainer(c);
            }
        }
    }
}

// Postfix ZNetScene Awake to get all containers loaded by the game.
[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
internal static class ZNetSceneAwakePatch
{
    private static void Postfix(ZNetScene __instance)
    {
        foreach (Container? container in Resources.FindObjectsOfTypeAll<Container>())
        {
            Functions.LogIfBuildDebug($"Found container by the name of {container.name} in your game.");
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateTeleport))]
public static class PlayerUpdateTeleportPatchCleanupContainers
{
    public static void Prefix(float dt)
    {
        if (Player.m_localPlayer == null || !Player.m_localPlayer.m_teleporting)
            return;
        foreach (Container? container in Boxes.Containers.ToList().Where(container => container == null || container.transform == null || container.GetInventory() == null))
        {
            Boxes.RemoveContainer(container);
        }
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll), typeof(Inventory), typeof(bool))]
#if DEBUG
[HarmonyEmitIL]
#endif
public static class Inventory_StackAll_Patch
{
    // Vanilla builds `new List<ItemData>(fromInventory.GetAllItems())`. Splice one
    // call in between, so the list is filtered before the copy is constructed:
    //     callvirt GetAllItems -> call FilterItems -> newobj List(IEnumerable)
    // The earlier version stored through local slot 3 and dropped the newobj, which
    // depended on the vanilla method's local layout. Valheim 1.0 renumbered those
    // slots. Operating purely on the evaluation stack removes that dependency.
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = [..instructions];
        MethodInfo? getAllItemsMethod = typeof(Inventory).GetMethod(nameof(Inventory.GetAllItems), BindingFlags.Instance | BindingFlags.Public, null, [], null);
        MethodInfo filterMethod = typeof(Inventory_StackAll_Patch).GetMethod(nameof(FilterItems))!;

        int getAllItemsIndex = code.FindIndex(instr => instr.opcode == OpCodes.Callvirt && ReferenceEquals(instr.operand, getAllItemsMethod));

        if (getAllItemsIndex == -1)
        {
            throw new Exception("Could not find GetAllItems call");
        }

        code.Insert(getAllItemsIndex + 1, new CodeInstruction(OpCodes.Call, filterMethod));
        return code.AsEnumerable();
    }

    public static List<ItemDrop.ItemData> FilterItems(List<ItemDrop.ItemData> items)
    {
        return items.Where(ShouldIncludeItem).ToList();
    }

    public static bool ShouldIncludeItem(ItemDrop.ItemData item)
    {
        if (!Player.m_localPlayer) return true;
        return !VanillaContainers.CantStoreFavorite(item, UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID()));
    }
}