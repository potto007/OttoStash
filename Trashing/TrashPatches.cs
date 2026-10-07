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

    /// The game picks an item up on pointer down (or the gamepad's A) through
    /// OnSelectedItem, so marking has to happen there, before the pickup. Returns
    /// false when the press marked a slot.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    private static bool InventoryGuiOnSelectedItemPrefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData? item, Vector2i pos)
    {
        if (grid != __instance.m_playerGrid || __instance.m_dragGo || item == null || !IsTrashing || Player.m_localPlayer.IsTeleporting())
            return true;
        ToggleMark(pos);
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
