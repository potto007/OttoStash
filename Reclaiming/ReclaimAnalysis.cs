using System.Text;

namespace OttoStash.Reclaiming;

/// One item's reclaim check: the recipe found for it, what it would return,
/// what blocks reclaiming it, and what keeps it out of the Reclaim tab.
internal sealed class ReclaimAnalysis
{
    internal readonly ItemDrop.ItemData Item;
    internal Recipe? Recipe;
    internal readonly List<string> RecyclingImpediments = new();
    internal readonly List<string> DisplayImpediments = new();
    internal readonly List<ReclaimYield> Entries = new();
    internal bool ShouldErrorDumpAnalysis;

    internal ReclaimAnalysis(ItemDrop.ItemData item)
    {
        Item = item;
    }

    /// Writes the analysis to the log, when an error asked for it.
    internal void Dump()
    {
        if (!ShouldErrorDumpAnalysis)
            return;

        StringBuilder sb = new();
        sb.AppendLine("\n==== Reclaim analysis ====");
        sb.AppendLine($"Item: {Item.m_shared.m_name} quality {Item.m_quality} stack {Item.m_stack}/{Item.m_shared.m_maxStackSize}");
        foreach (string impediment in RecyclingImpediments)
            sb.AppendLine($"Impediment: {impediment}");
        if (Recipe != null)
        {
            sb.AppendLine($"Recipe: {Recipe.name} crafts {Recipe.m_amount}x {Recipe.m_item?.m_itemData.m_shared.m_name}");
            foreach (Piece.Requirement resource in Recipe.m_resources)
                sb.AppendLine($"  Resource: {resource.m_resItem?.m_itemData.m_shared.m_name} amount {resource.m_amount} per level {resource.m_amountPerLevel}");
        }

        foreach (ReclaimYield entry in Entries)
            sb.AppendLine($"Yield: {entry.Amount}x {entry.Prefab.name} quality {entry.Quality} variant {entry.Variant}");
        sb.AppendLine("==== End of reclaim analysis ====");
        StashLog.Error(sb.ToString());
    }
}

internal readonly struct ReclaimYield
{
    internal readonly GameObject Prefab;
    internal readonly ItemDrop.ItemData RecipeItemData;
    internal readonly int Amount;
    internal readonly int Quality;
    internal readonly int Variant;
    internal readonly bool InitialRecipeHadZero;

    internal ReclaimYield(GameObject prefab, ItemDrop.ItemData itemData, int amount, int quality, int variant, bool initialRecipeHadZero)
    {
        Prefab = prefab;
        RecipeItemData = itemData;
        Amount = amount;
        Quality = quality;
        Variant = variant;
        InitialRecipeHadZero = initialRecipeHadZero;
    }
}

/// The yield arithmetic, free of game state so it can be tested.
internal static class ReclaimMath
{
    /// How much of one resource a reclaim returns. amountPerLevelSum is what the
    /// item's quality levels cost of the resource, stackFraction the item's stack
    /// over the recipe's crafted amount. Always rounds down.
    internal static (int Amount, bool InitialRecipeHadZero) FinalAmount(int amountPerLevelSum, double stackFraction, float recyclingRate,
        bool unstackable, bool unstackableReturnsAtLeastOne)
    {
        if (amountPerLevelSum == 0)
            return (0, true);

        int amount = (int)Math.Floor(amountPerLevelSum * stackFraction * recyclingRate);
        if (amount < 1 && unstackable && unstackableReturnsAtLeastOne)
            amount = 1;
        return (amount, false);
    }

    /// The quality levels whose cost an item has paid for one requirement. An
    /// upgrader resource only counts the levels past the item's normal maximum.
    internal static IEnumerable<int> SpentQualityLevels(bool upgraderResource, int maxQuality, int quality)
    {
        return upgraderResource
            ? Enumerable.Range(maxQuality + 1, Math.Max(0, quality - maxQuality))
            : Enumerable.Range(1, Math.Max(0, Math.Min(quality, maxQuality)));
    }
}
