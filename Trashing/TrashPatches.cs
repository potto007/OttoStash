using OttoStash.Reclaiming;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OttoStash.Trashing;

/// Trash marking: hold TrashingModifierKeybind and left click inventory slots to
/// mark them, then press TrashingKeybind to discard every marked slot at once.
/// Letting go of the modifier, or closing the inventory, clears the marks.
[HarmonyPatch]
internal static class TrashPatches
{
    private const string BorderName = "OttoStashTrashBorder";
    internal static Sprite BorderSprite = null!;
    private static readonly HashSet<Vector2i> MarkedSlots = new();

    internal static bool IsTrashing => TrashingModifierKeybind.Value.IsKeyHeld();

    internal static bool IsMarked(Vector2i slot) => MarkedSlots.Contains(slot);

    private static void ClearMarks() => MarkedSlots.Clear();

    private static void ToggleMark(Vector2i slot)
    {
        if (!MarkedSlots.Remove(slot))
            MarkedSlots.Add(slot);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.OnLeftClick))]
    private static bool InventoryGridOnLeftClickPrefix(InventoryGrid __instance, UIInputHandler clickHandler)
    {
        return HandleClick(__instance, __instance.GetButtonPos(clickHandler.gameObject));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGamepad))]
    private static void InventoryGridUpdateGamepadPostfix(InventoryGrid __instance)
    {
        if (__instance != InventoryGui.instance.m_playerGrid || !__instance.m_uiGroup.IsActive)
            return;
        if (ZInput.GetButtonDown("JoyButtonA"))
            HandleClick(__instance, __instance.m_selected);
    }

    /// False when the click marked a slot, so the game does not also pick the item up.
    private static bool HandleClick(InventoryGrid grid, Vector2i slot)
    {
        if (InventoryGui.instance.m_playerGrid != grid || Player.m_localPlayer.IsTeleporting() || InventoryGui.instance.m_dragGo
            || !IsTrashing || slot == new Vector2i(-1, -1))
            return true;

        bool gamepad = grid.m_uiGroup.IsActive && ZInput.IsGamepadActive();
        InventoryElement? element = gamepad ? grid.GetElement(grid.m_selected.x, grid.m_selected.y, grid.m_inventory.GetWidth()) : grid.GetHoveredElement();
        if (element is { m_used: true })
            ToggleMark(slot);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.LowerThanNormal)]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static void InventoryGuiHidePostfix()
    {
        ClearMarks();
    }

    /// Draws the borders, and runs the bulk discard when its key is pressed.
    [HarmonyPostfix]
    [HarmonyAfter("goldenrevolver.quick_stack_store")]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    private static void InventoryGridUpdateGuiPostfix(Player player, Inventory ___m_inventory, List<InventoryElement> ___m_elements)
    {
        if (player == null || player.m_inventory != ___m_inventory)
            return;

        int width = ___m_inventory.GetWidth();
        for (int y = 0; y < ___m_inventory.GetHeight(); ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                Image border = BorderOf(___m_elements[y * width + x]);
                border.color = BorderColorTrashedSlot.Value;
                border.enabled = IsMarked(new Vector2i(x, y));
            }
        }

        if (!IsTrashing)
        {
            ClearMarks();
            return;
        }

        if (!TrashingKeybind.Value.IsKeyDown() || MarkedSlots.Count == 0)
            return;

        if (DiscardAllowed)
        {
            foreach (ItemDrop.ItemData item in ___m_inventory.GetAllItems().Where(item => IsMarked(item.m_gridPos)).ToList())
                InventoryDiscard.Discard(item, item.m_stack, ___m_inventory);
        }

        ClearMarks();
    }

    private static Image BorderOf(InventoryElement element)
    {
        Transform queued = element.m_queued.transform;
        Transform? existing = queued.childCount > 0 ? global::Utils.FindChild(queued, BorderName) : null;
        return existing != null ? existing.GetComponent<Image>() : CreateBorder(element.m_queued);
    }

    /// A copy of the slot's queued image, stripped of what other mods added to it,
    /// parented to it so it can be found again without a search.
    private static Image CreateBorder(Image baseImage)
    {
        Image border = Object.Instantiate(baseImage, baseImage.transform.parent);
        foreach (Transform child in border.transform)
            Object.Destroy(child.gameObject);
        border.name = BorderName;
        border.transform.SetParent(baseImage.transform);
        border.sprite = BorderSprite;
        return border;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    private static void ItemDataGetTooltipPostfix(ItemDrop.ItemData item, bool crafting, ref string __result)
    {
        if (crafting || !TrashTooltipHint.Value || Player.m_localPlayer == null || !IsMarked(item.m_gridPos)
            || !Player.m_localPlayer.GetInventory().ContainsItem(item))
            return;
        __result += $"{Environment.NewLine}<color=#{ColorUtility.ToHtmlStringRGB(BorderColorTrashedSlot.Value)}>{TrashedSlotTooltip.Value}</color>";
    }
}
