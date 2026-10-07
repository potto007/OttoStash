namespace OttoStash.Pulling;

/// A Jewelcrafting gem bag the player carries, as a source of materials. Only
/// bags Jewelcrafting reports as freely accessible are ever made into one.
internal sealed class GemBagSource : IPullSource
{
    private const string FallbackRuleName = "JC_Gem_Bag";

    private readonly ItemDrop.ItemData _bag;

    internal GemBagSource(ItemDrop.ItemData bag)
    {
        _bag = bag;
        RuleName = bag.m_dropPrefab != null ? bag.m_dropPrefab.name : FallbackRuleName;
    }

    public string RuleName { get; }

    public Inventory? Inventory => Jewelcrafting.API.GetItemContainerInventory(_bag);

    public int Count(string sharedName, int quality)
    {
        return Inventory?.CountItems(sharedName, quality) ?? 0;
    }

    public int Take(string sharedName, int quality, int amount)
    {
        Inventory? inventory = Inventory;
        if (inventory == null || amount <= 0)
            return 0;

        int taken = Math.Min(amount, inventory.CountItems(sharedName, quality));
        if (taken <= 0)
            return 0;

        inventory.RemoveItem(sharedName, taken, quality);
        StashLog.Debug($"Pulled {taken} {sharedName} from {RuleName}");
        return taken;
    }
}
