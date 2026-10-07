using System.Text;

namespace OttoStash.Reclaiming;

/// With ShowRecycleYieldInTooltip on, an item's tooltip lists what reclaiming it
/// would return, and what would block it.
[HarmonyPatch]
internal static class YieldTooltip
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip), typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool))]
    private static void ItemDataGetTooltipPostfix(ItemDrop.ItemData item, bool crafting, ref string __result)
    {
        if (crafting || !ShowRecycleYieldInTooltip.Value || Player.m_localPlayer == null)
            return;

        ReclaimAnalysis analysis = new(item);
        if (!Reclaimer.TryAnalyze(analysis, Player.m_localPlayer.GetInventory(), Player.m_localPlayer))
            return;

        List<ReclaimYield> yields = analysis.Entries.Where(entry => entry.Amount > 0).ToList();
        if (yields.Count == 0)
            return;

        StringBuilder sb = new(__result);
        sb.Append($"\n\n<color=orange>{ReclaimText.Localize("$ottostash_reclaim_tooltip_yield_header")}</color>");
        foreach (ReclaimYield entry in yields)
            sb.Append($"\n  {entry.Amount}x {ReclaimText.Localize(entry.RecipeItemData.m_shared.m_name)}");
        if (analysis.RecyclingImpediments.Count > 0)
            sb.Append($"\n<color=red>{string.Join(", ", analysis.RecyclingImpediments)}</color>");
        __result = sb.ToString();
    }
}
