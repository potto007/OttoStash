using OttoStash.APIs;

namespace OttoStash.Pulling;

/// A kg ItemDrawers drawer as a source of materials. A drawer holds one prefab
/// at one quality, and its count is read once when the drawer is found.
internal sealed class KgDrawerSource : IPullSource
{
    private readonly ItemDrawers_API.Drawer _drawer;
    private readonly string? _sharedName;
    private int _remaining;

    internal KgDrawerSource(ItemDrawers_API.Drawer drawer)
    {
        _drawer = drawer;
        _remaining = drawer.Amount;
        RuleName = PrefabNames.FromSceneName(drawer.gameObject.name);

        GameObject? prefab = ObjectDB.instance != null && !string.IsNullOrEmpty(drawer.Prefab) ? ObjectDB.instance.GetItemPrefab(drawer.Prefab) : null;
        _sharedName = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name : null;
    }

    public string RuleName { get; }

    public Inventory? Inventory => null;

    public int Count(string sharedName, int quality)
    {
        if (_sharedName == null || _sharedName != sharedName)
            return 0;
        if (quality >= 0 && quality != _drawer.Quality)
            return 0;
        return Math.Max(0, _remaining);
    }

    public int Take(string sharedName, int quality, int amount)
    {
        int taken = Math.Min(amount, Count(sharedName, quality));
        if (taken <= 0)
            return 0;

        _drawer.Remove(taken);
        _remaining -= taken;
        StashLog.Debug($"Pulled {taken} {sharedName} from {_drawer.gameObject.name}");
        return taken;
    }
}
