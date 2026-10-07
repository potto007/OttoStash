using System.Reflection.Emit;

namespace OttoStash.Patches;

/// The player-triggered stores: the single-item hotkey over an inventory slot,
/// and keeping favorited items out of the vanilla stack-all.
[HarmonyPatch]
internal static class StorePatches
{
    // The hotkey is read while the grid updates, so the hovered or gamepad-selected
    // slot is known.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    private static void InventoryGridUpdateGuiPrefix(InventoryGrid __instance)
    {
        bool gamepad = __instance.m_uiGroup.IsActive && ZInput.IsGamepadActive();
        InventoryElement? element = gamepad
            ? __instance.GetElement(__instance.m_selected.x, __instance.m_selected.y, __instance.m_inventory.GetWidth())
            : __instance.GetHoveredElement();
        if (element == null)
            return;

        if (!SingleItemShortcut.Value.IsKeyDown())
            return;

        // Both default to the middle mouse button; with the trash modifier held it
        // means bulk trash, not store.
        if (TrashingModifierKeybind.Value.IsKeyHeld())
            return;

        if (__instance.m_inventory == null)
            return;

        Vector2i position = __instance.GetElementPos(element);
        ItemDrop.ItemData? item = __instance.m_inventory.GetItemAt(position.x, position.y);
        if (item == null)
            return;

        PlayerStore.StoreItem(item, __instance.m_inventory);
    }

    // Vanilla builds `new List<ItemData>(fromInventory.GetAllItems())`. One call is
    // spliced in between, so the list is filtered before the copy is constructed:
    //     callvirt GetAllItems -> call FilterItems -> newobj List(IEnumerable)
    // An earlier version stored through local slot 3 and dropped the newobj, which
    // depended on the vanilla method's local layout. Valheim 1.0 renumbered those
    // slots. Operating purely on the evaluation stack removes that dependency.
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll), typeof(Inventory), typeof(bool))]
#if DEBUG
    [HarmonyEmitIL]
#endif
    private static IEnumerable<CodeInstruction> InventoryStackAllTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();
        MethodInfo? getAllItems = typeof(Inventory).GetMethod(nameof(Inventory.GetAllItems), BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        MethodInfo filter = typeof(StorePatches).GetMethod(nameof(FilterItems))!;

        int index = code.FindIndex(instruction => instruction.opcode == OpCodes.Callvirt && ReferenceEquals(instruction.operand, getAllItems));
        if (index == -1)
            throw new Exception("Could not find the GetAllItems call in Inventory.StackAll.");

        code.Insert(index + 1, new CodeInstruction(OpCodes.Call, filter));
        return code;
    }

    /// Called from the transpiled method, so it must stay public and static.
    public static List<ItemDrop.ItemData> FilterItems(List<ItemDrop.ItemData> items)
    {
        return items.Where(ShouldInclude).ToList();
    }

    private static bool ShouldInclude(ItemDrop.ItemData item)
    {
        if (Player.m_localPlayer == null)
            return true;
        return !PlayerFavorites.For(Player.m_localPlayer.GetPlayerID()).IsItemOrSlotFavorited(item);
    }
}
