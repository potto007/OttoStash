using Backpacks;

namespace OttoStash.Pulling;

/// A Backpacks-mod backpack the player carries, as a source of materials.
internal sealed class BackpackSource : IPullSource
{
    private readonly ItemContainer _backpack;

    internal BackpackSource(ItemContainer backpack)
    {
        _backpack = backpack;
        RuleName = backpack.Item.m_dropPrefab != null ? backpack.Item.m_dropPrefab.name : backpack.Item.m_shared.m_name;
    }

    public string RuleName { get; }

    public Inventory? Inventory => _backpack.Inventory;

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
        try
        {
            _backpack.Save();
        }
        catch
        {
            // The backpack mod saves on its own schedule; a failed early save is harmless.
        }

        StashLog.Debug($"Pulled {taken} {sharedName} from {RuleName}");
        return taken;
    }
}
