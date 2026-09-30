using System.Text;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OttoStash.Favoriting;

/// Favoriting in the inventory screen: the clicks and gamepad buttons that
/// toggle it, the coloured borders that show it, and the tooltip line.
[HarmonyPatch]
internal static class FavoritingPatches
{
    private const string BorderName = "OttoStashFavoritingBorder";

    /// The border image, loaded from the embedded asset at startup.
    internal static Sprite BorderSprite = null!;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.OnRightDown))]
    private static bool InventoryGridOnRightDownPrefix(InventoryGrid __instance, UIInputHandler element)
    {
        return HandleClick(__instance, __instance.GetButtonPos(element.gameObject), isLeftClick: false, SearchModifierKeybind.Value.IsKeyHeld());
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.OnLeftDown))]
    private static bool InventoryGridOnLeftDownPrefix(InventoryGrid __instance, UIInputHandler clickHandler)
    {
        return HandleClick(__instance, __instance.GetButtonPos(clickHandler.gameObject), isLeftClick: true, SearchModifierKeybind.Value.IsKeyHeld());
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGamepad))]
    private static void InventoryGridUpdateGamepadPostfix(InventoryGrid __instance)
    {
        if (__instance != InventoryGui.instance.m_playerGrid || !__instance.m_uiGroup.IsActive)
            return;

        if (ZInput.GetButtonDown("JoyButtonA"))
            HandleClick(__instance, __instance.m_selected, isLeftClick: true);
        else if (ZInput.GetButtonDown("JoyButtonX"))
            HandleClick(__instance, __instance.m_selected, isLeftClick: false);
    }

    // Returns false to swallow the click when favoriting handled it.
    private static bool HandleClick(InventoryGrid grid, Vector2i buttonPos, bool isLeftClick, bool searchInstead = false)
    {
        if (InventoryGui.instance.m_playerGrid != grid)
            return true;

        Player localPlayer = Player.m_localPlayer;
        if (localPlayer.IsTeleporting() || InventoryGui.instance.m_dragGo)
            return true;

        if (!FavoritingMode.IsActive())
            return true;

        if (buttonPos == new Vector2i(-1, -1))
            return true;

        if (!isLeftClick)
        {
            PlayerFavorites.For(localPlayer.GetPlayerID()).ToggleSlot(buttonPos);
            return false;
        }

        ItemDrop.ItemData? item = grid.m_inventory.GetItemAt(buttonPos.x, buttonPos.y);
        if (item == null)
            return true;

        if (searchInstead)
        {
            Chat.instance.TryRunCommand($"{ContainerSearch.CommandName} {item.m_dropPrefab.name.ToLower()}", false, true);
            return false;
        }

        PlayerFavorites.For(localPlayer.GetPlayerID()).ToggleItemName(item.m_shared);
        return false;
    }

    // Slightly lower priority so the borders render on top of equipment slot mods.
    [HarmonyPostfix]
    [HarmonyPriority(Priority.LowerThanNormal)]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    private static void InventoryGuiShowPostfix(InventoryGui __instance)
    {
        // The toggle does not survive closing the screen, in case the player forgot it.
        if (__instance == InventoryGui.instance && Player.m_localPlayer != null)
            FavoritingMode.Toggled = false;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.LowerThanNormal)]
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    private static void InventoryGuiHidePostfix()
    {
        FavoritingMode.Toggled = false;
    }

    [HarmonyPostfix]
    [HarmonyAfter("goldenrevolver.quick_stack_store")]
    [HarmonyPriority(Priority.LowerThanNormal)]
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    private static void InventoryGridUpdateGuiPostfix(InventoryGrid __instance, Player player, Inventory ___m_inventory, List<InventoryElement> ___m_elements)
    {
        if (player == null || player.m_inventory != ___m_inventory)
            return;

        int width = ___m_inventory.GetWidth();
        PlayerFavorites favorites = PlayerFavorites.For(player.GetPlayerID());

        for (int y = 0; y < ___m_inventory.GetHeight(); ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                Image border = BorderOf(___m_elements[y * width + x]);
                border.color = BorderColorFavoritedSlot.Value;
                border.enabled = favorites.IsSlotFavorited(new Vector2i(x, y));
            }
        }

        foreach (ItemDrop.ItemData item in ___m_inventory.m_inventory)
        {
            if (!favorites.IsItemNameFavorited(item.m_shared))
                continue;

            Image border = BorderOf(___m_elements[item.m_gridPos.y * width + item.m_gridPos.x]);
            // An enabled border at this point means the slot itself is favorited too.
            border.color = border.enabled ? BorderColorFavoritedItemOnFavoritedSlot.Value : BorderColorFavoritedItem.Value;
            border.enabled = true;
        }
    }

    private static Image BorderOf(InventoryElement element)
    {
        Transform queued = element.m_queued.transform;
        Transform? existing = queued.childCount > 0 ? Utils.FindChild(queued, BorderName) : null;
        return existing != null ? existing.GetComponent<Image>() : CreateBorder(element.m_queued);
    }

    // The border is a copy of the slot's queued image, parented to it so the
    // position matches, with the copy's own children removed.
    private static Image CreateBorder(Image baseImage)
    {
        Image border = Object.Instantiate(baseImage, baseImage.transform);
        foreach (Transform child in border.transform)
            Object.Destroy(child.gameObject);
        border.name = BorderName;
        border.transform.SetParent(baseImage.transform);
        border.sprite = BorderSprite;
        return border;
    }

    // Valheim 1.0 added the trailing `appending` parameter. Harmony resolves this
    // target by name at run time, so a stale list compiles and then fails on load.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    private static void ItemDataGetTooltipPostfix(ItemDrop.ItemData item, bool crafting, bool appending, ref string __result)
    {
        // The game calls itself with appending: true to build a nested tooltip.
        // Adding the hint there would print it twice.
        if (crafting || appending || !DisplayTooltipHint.Value || Player.m_localPlayer == null)
            return;

        PlayerFavorites favorites = PlayerFavorites.For(Player.m_localPlayer.GetPlayerID());
        bool itemFavorited = favorites.IsItemNameFavorited(item.m_shared);
        bool slotFavorited = favorites.IsSlotFavorited(item.m_gridPos);

        string? hint = null;
        if (itemFavorited && slotFavorited)
            hint = Coloured(ItemOnFavoritedSlotTooltip.Value, BorderColorFavoritedItemOnFavoritedSlot.Value);
        else if (itemFavorited)
            hint = Coloured(FavoritedItemTooltip.Value, BorderColorFavoritedItem.Value);
        else if (slotFavorited)
            hint = Coloured(FavoritedSlotTooltip.Value, BorderColorFavoritedSlot.Value);

        if (hint == null)
            return;

        StringBuilder tooltip = new(256);
        tooltip.Append(__result);
        tooltip.Append(Environment.NewLine);
        tooltip.Append(hint);
        __result = tooltip.ToString();
    }

    private static string Coloured(string text, Color colour)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(colour)}>{text}</color>";
    }
}
