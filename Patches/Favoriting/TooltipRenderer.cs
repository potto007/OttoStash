using System.Text;

namespace OttoStash.Patches.Favoriting;

[HarmonyPatch(typeof(ItemDrop.ItemData))]
internal static class TooltipRenderer
{
    // Valheim 1.0 added the trailing `appending` parameter. Harmony resolves this
    // target by name at run time, so a stale list compiles and then fails on load.
    [HarmonyPatch(nameof(ItemDrop.ItemData.GetTooltip), [
        typeof(ItemDrop.ItemData),
        typeof(int),
        typeof(bool),
        typeof(float),
        typeof(int),
        typeof(bool)
    ])]
    [HarmonyPostfix]
    public static void GetTooltip(ItemDrop.ItemData item, bool crafting, bool appending, ref string __result)
    {
        // The game calls itself with appending: true to build a nested tooltip.
        // Adding the hint there would print it twice.
        if (crafting || appending || !DisplayTooltipHint.Value || !Player.m_localPlayer)
        {
            return;
        }
        StringBuilder stringBuilder = new StringBuilder(256);
        stringBuilder.Append(__result);

        UserConfig conf = UserConfig.GetPlayerConfig(Player.m_localPlayer.GetPlayerID());

        if (conf.IsItemNameFavorited(item.m_shared))
        {
            string? color = ColorUtility.ToHtmlStringRGB(BorderColorFavoritedItem.Value);

            stringBuilder.Append($"{Environment.NewLine}<color=#{color}>{FavoritedItemTooltip.Value}</color>");
        }
        else if (conf.IsSlotFavorited(item.m_gridPos))
        {
            string? color = ColorUtility.ToHtmlStringRGB(BorderColorFavoritedSlot.Value);

            stringBuilder.Append($"{Environment.NewLine}<color=#{color}>{FavoritedSlotTooltip.Value}</color>");
        }
        else if (conf.IsSlotFavorited(item.m_gridPos) && conf.IsItemNameFavorited(item.m_shared))
        {
            string? color = ColorUtility.ToHtmlStringRGB(BorderColorFavoritedItemOnFavoritedSlot.Value);

            stringBuilder.Append($"{Environment.NewLine}<color=#{color}>{ItemOnFavoritedSlotTooltip.Value}</color>");
        }

        __result = stringBuilder.ToString();
    }
}