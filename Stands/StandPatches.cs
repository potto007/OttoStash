namespace OttoStash.Stands;

/// The armor stand panel: Use on a stand opens the inventory screen with the stand's
/// slots beside it; drag, Ctrl-click and Take All move gear both ways. The hotbar keys
/// still attach the vanilla way.
[HarmonyPatch]
internal static class StandPatches
{
    private static bool Enabled => ArmorStandPanel.Value.IsOn();

    // Vanilla takes the slot's item on a bare Use and throws it on the ground. With
    // the panel on, a bare Use opens the panel instead. A hotbar item still attaches.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ArmorStand), nameof(ArmorStand.UseItem))]
    private static bool ArmorStandUseItemPrefix(ArmorStand __instance, Humanoid user, ItemDrop.ItemData? item, ref bool __result)
    {
        if (!Enabled || item != null || user != Player.m_localPlayer)
            return true;
        if (__instance.m_nview == null || !__instance.m_nview.IsValid())
            return true;

        __result = true;
        if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: true))
            return false;

        StandSession.Open(__instance);
        return false;
    }

    // The hover text names what Use does, which is now opening the panel.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ArmorStand), nameof(ArmorStand.Awake))]
    private static void ArmorStandAwakePostfix(ArmorStand __instance)
    {
        if (__instance.m_nview == null || __instance.m_nview.GetZDO() == null)
            return;

        foreach (ArmorStand.ArmorStandSlot slot in __instance.m_slots)
        {
            Switch? slotSwitch = slot.m_switch;
            if (slotSwitch == null)
                continue;
            slotSwitch.m_onHover = () => HoverText(__instance, slotSwitch);
        }
    }

    private static string HoverText(ArmorStand stand, Switch slotSwitch)
    {
        if (!PrivateArea.CheckAccess(stand.transform.position, 0f, flash: false))
            return Localization.instance.Localize(stand.m_name + "\n$piece_noaccess");

        string use = Enabled
            ? "\n[<color=yellow><b>$KEY_Use</b></color>] Open"
            : stand.GetNrOfAttachedItems() > 0 ? "\n[<color=yellow><b>$KEY_Use</b></color>] $piece_itemstand_take" : "";
        return Localization.instance.Localize(slotSwitch.m_hoverText + "\n[<color=yellow><b>$KEY_HotbarUse</b></color>] $piece_itemstand_attach" + use);
    }

    // The container side of the screen shows the mirror while a stand is open.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    private static bool InventoryGuiUpdateContainerPrefix(InventoryGui __instance, Player player)
    {
        StandSession? session = StandSession.Current;
        if (session == null)
            return true;
        if (!__instance.m_animator.GetBool("visible"))
            return false;
        if (__instance.m_currentContainer != null)
        {
            // A chest opened over the stand; vanilla takes the panel back.
            EndSession(__instance);
            return true;
        }

        if (!session.IsValid || Vector3.Distance(session.Stand.transform.position, player.transform.position) > __instance.m_autoCloseDistance)
        {
            EndSession(__instance);
            __instance.m_container.gameObject.SetActive(false);
            return false;
        }

        session.Tick();
        __instance.m_container.gameObject.SetActive(true);
        __instance.m_containerGrid.UpdateInventory(session.Mirror, null, __instance.m_dragItem);
        __instance.m_containerName.text = session.Name;
        if (__instance.m_firstContainerUpdate)
        {
            __instance.m_containerGrid.ResetView();
            __instance.m_firstContainerUpdate = false;
        }
        return false;
    }

    private static void EndSession(InventoryGui gui)
    {
        StandSession? session = StandSession.Current;
        if (session != null && gui.m_dragInventory == session.Mirror)
            gui.SetupDragItem(null, null, 1);
        StandSession.Close();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static void InventoryGuiHidePostfix()
    {
        StandSession.Close();
    }

    // Gamepad group navigation and the panel's own checks ask this.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.IsContainerOpen))]
    private static void InventoryGuiIsContainerOpenPostfix(ref bool __result)
    {
        if (StandSession.Current != null)
            __result = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainerWeight))]
    private static bool InventoryGuiUpdateContainerWeightPrefix(InventoryGui __instance)
    {
        StandSession? session = StandSession.Current;
        if (session == null)
            return true;
        __instance.m_containerWeight.text = Mathf.CeilToInt(session.Mirror.GetTotalWeight()).ToString();
        return false;
    }

    // Ctrl-click: a stand item goes to the inventory, an inventory item goes to the
    // first slot that takes it. Vanilla would drop the item on the ground here,
    // because it has no container.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    private static bool InventoryGuiOnSelectedItemPrefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData? item, InventoryGrid.Modifier mod)
    {
        StandSession? session = StandSession.Current;
        if (session == null || __instance.m_dragGo != null || item == null || mod != InventoryGrid.Modifier.Move)
            return true;

        Player player = Player.m_localPlayer;
        if (player == null || player.IsTeleporting())
            return true;
        Inventory from = grid.GetInventory();
        if (from != session.Mirror && from != player.GetInventory())
            return true;
        if (item.m_shared.m_questItem)
            return false;
        if (!session.CanMove())
            return false;

        bool moved;
        if (from == session.Mirror)
        {
            player.GetInventory().MoveItemToThis(session.Mirror, item);
            moved = !session.Mirror.ContainsItem(item);
        }
        else
        {
            player.RemoveEquipAction(item);
            player.UnequipItem(item);
            moved = session.TryQuickLoad(item, from);
            if (!moved)
                player.Message(MessageHud.MessageType.Center, "$piece_armorstand_cantattach");
        }

        if (moved)
            __instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTakeAll))]
    private static bool InventoryGuiOnTakeAllPrefix(InventoryGui __instance)
    {
        StandSession? session = StandSession.Current;
        if (session == null)
            return true;
        Player player = Player.m_localPlayer;
        if (player == null || player.IsTeleporting())
            return false;
        __instance.SetupDragItem(null, null, 1);
        if (session.CanMove())
            session.TakeAll(player.GetInventory());
        return false;
    }

    // Nothing on a stand stacks.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnStackAll))]
    private static bool InventoryGuiOnStackAllPrefix()
    {
        return StandSession.Current == null;
    }

    // Every drop that touches the mirror passes here: the drag release, and the
    // swap when the target cell holds something. The stand must be owned, the cell
    // must be a slot, and the item must be one the slot takes.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
    private static bool InventoryGridDropItemPrefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, int amount, Vector2i pos, ref bool __result)
    {
        StandSession? session = StandSession.Current;
        if (session == null)
            return true;

        bool toMirror = __instance.m_inventory == session.Mirror;
        bool fromMirror = fromInventory == session.Mirror;
        if (!toMirror && !fromMirror)
            return true;

        __result = false;
        if (!session.CanMove())
            return false;
        if (!toMirror)
            return true;

        ItemDrop.ItemData? occupant = session.Mirror.GetItemAt(pos.x, pos.y);
        if (occupant == item)
        {
            __result = true;
            return false;
        }
        if (!session.AcceptsAt(pos, item, amount))
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$piece_armorstand_cantattach");
            return false;
        }
        // A swap inside the stand sends the occupant to the dragged item's old slot.
        if (occupant != null && fromMirror && !session.AcceptsAt(item.m_gridPos, occupant, 1))
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "$piece_armorstand_cantattach");
            return false;
        }
        return true;
    }

    // Cells no slot claims are hidden, so the grid reads as a figure. An empty slot
    // cell says what it takes.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    private static void InventoryGridUpdateGuiPostfix(InventoryGrid __instance)
    {
        StandSession? session = StandSession.Current;
        List<InventoryElement> elements = __instance.m_elements;

        if (session == null || __instance.m_inventory != session.Mirror)
        {
            // A chest grid of the same size reuses the cells the stand hid.
            if (InventoryGui.instance != null && __instance == InventoryGui.instance.m_containerGrid)
            {
                foreach (InventoryElement element in elements)
                {
                    if (!element.gameObject.activeSelf)
                        element.gameObject.SetActive(true);
                }
            }
            return;
        }

        for (int i = 0; i < elements.Count; i++)
        {
            InventoryElement element = elements[i];
            int slot = session.SlotAtCellIndex(i);
            bool shown = slot >= 0;
            if (element.gameObject.activeSelf != shown)
                element.gameObject.SetActive(shown);
            if (shown && !element.m_used)
            {
                element.m_tooltip.m_topic = session.SlotLabel(slot);
                element.m_tooltip.m_text = "";
            }
        }
    }

    // Right-click on a stand item would try to equip it out of the mirror.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem), typeof(Inventory), typeof(ItemDrop.ItemData), typeof(bool))]
    private static bool HumanoidUseItemPrefix(Inventory? inventory)
    {
        StandSession? session = StandSession.Current;
        return session == null || inventory != session.Mirror;
    }

    // Dropping a stand item on the ground clears its slot, so it needs ownership too.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.DropItem), typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int))]
    private static bool HumanoidDropItemPrefix(Inventory? inventory, ref bool __result)
    {
        StandSession? session = StandSession.Current;
        if (session == null || inventory != session.Mirror || session.CanMove())
            return true;
        __result = false;
        return false;
    }
}
