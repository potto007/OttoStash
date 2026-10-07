namespace OttoStash.Pulling;

/// A registered vanilla chest as a source of materials.
internal sealed class ChestSource : IPullSource
{
    private readonly Container _chest;

    internal ChestSource(Container chest)
    {
        _chest = chest;
        RuleName = PrefabNames.FromSceneName(chest.gameObject.name);
    }

    public string RuleName { get; }

    public Inventory? Inventory => _chest != null ? _chest.GetInventory() : null;

    public int Count(string sharedName, int quality)
    {
        return Inventory?.CountItems(sharedName, quality) ?? 0;
    }

    /// The chest only saves a change while this client owns it, so ownership is
    /// claimed first, the same way a store claims a chest before filling it.
    public int Take(string sharedName, int quality, int amount)
    {
        Inventory? inventory = Inventory;
        if (inventory == null || amount <= 0)
            return 0;

        int taken = Math.Min(amount, inventory.CountItems(sharedName, quality));
        if (taken <= 0)
            return 0;

        ClaimOwnership();
        inventory.RemoveItem(sharedName, taken, quality);
        StashLog.Debug($"Pulled {taken} {sharedName} from {_chest.name}");
        return taken;
    }

    internal void ClaimOwnership()
    {
        ZNetView? view = _chest.m_nview;
        if (view != null && view.IsValid() && !view.IsOwner())
            view.ClaimOwnership();
    }
}
