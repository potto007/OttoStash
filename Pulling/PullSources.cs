using Backpacks;
using ItemDataManager;

namespace OttoStash.Pulling;

/// The sources within pull range of the player: registered chests the player
/// may open, kg drawers, and the backpacks and gem bags they carry. The list is
/// rebuilt at most every quarter second while the player stands still, because
/// the crafting and build menus ask for it many times a frame.
internal static class PullSources
{
    private const float CacheSeconds = 0.25f;
    private const float StillDistanceSquared = 0.25f * 0.25f;

    private static readonly List<IPullSource> Cached = new();
    private static float _builtAt = float.NegativeInfinity;
    private static Vector3 _builtFrom;
    private static float _builtRange;

    internal static IReadOnlyList<IPullSource> Near(Player player, float range)
    {
        Vector3 position = player.transform.position;
        if (Time.time - _builtAt <= CacheSeconds && (position - _builtFrom).sqrMagnitude < StillDistanceSquared && Mathf.Approximately(range, _builtRange))
            return Cached;

        Cached.Clear();
        AddChests(position, range);
        Cached.AddRange(APIs.ItemDrawers_API.AllDrawersInRange(position, range).Select(drawer => new KgDrawerSource(drawer)));
        AddCarriedBags(player);

        _builtAt = Time.time;
        _builtFrom = position;
        _builtRange = range;
        return Cached;
    }

    /// Forgets the cached list, so the next query sees chests that were just
    /// built, destroyed or opened by someone else.
    internal static void Invalidate()
    {
        _builtAt = float.NegativeInfinity;
    }

    private static void AddChests(Vector3 position, float range)
    {
        long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
        float rangeSquared = range * range;

        foreach (Container chest in ContainerRegistry.Containers)
        {
            if (chest == null || chest.GetInventory() == null)
                continue;
            if ((chest.transform.position - position).sqrMagnitude > rangeSquared)
                continue;
            // A cart that someone is pulling is on the move.
            if (chest.m_wagon != null && chest.m_wagon.InUse())
                continue;
            if (ChestGate.IsOpenElsewhere(chest))
                continue;
            if (!chest.CheckAccess(playerId))
                continue;

            Cached.Add(new ChestSource(chest));
        }
    }

    private static void AddCarriedBags(Player player)
    {
        List<ItemDrop.ItemData> items = player.GetInventory().GetAllItems();

        if (BackpacksIsLoaded)
        {
            HashSet<ItemContainer> seen = new();
            foreach (ItemDrop.ItemData? item in items)
            {
                ItemContainer? backpack = item?.Data(BackpacksGuid)?.Get<ItemContainer>();
                if (backpack != null && seen.Add(backpack))
                    Cached.Add(new BackpackSource(backpack));
            }
        }

        if (Jewelcrafting.API.IsLoaded())
        {
            foreach (ItemDrop.ItemData? item in items)
            {
                if (item != null && Jewelcrafting.API.IsFreelyAccessibleInventory(item) && Jewelcrafting.API.GetItemContainerInventory(item) != null)
                    Cached.Add(new GemBagSource(item));
            }
        }
    }
}
