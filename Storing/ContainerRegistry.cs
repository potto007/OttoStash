using Backpacks;
using ItemDataManager;

namespace OttoStash.Storing;

/// The chests currently known to be autostore targets, and the lookup of every
/// kind of target within a range of a point.
internal static class ContainerRegistry
{
    internal static readonly List<Container> Containers = new();

    /// The targets that received items during the current store, for the
    /// ping and highlight afterwards.
    internal static readonly List<IStoreTarget> Filled = new();

    internal static void Add(Container container)
    {
        if (Containers.Contains(container))
            return;
        Containers.Add(container);
        StashLog.Debug($"Added container {container.name} to list");
    }

    internal static void Remove(Container container)
    {
        if (!Containers.Remove(container))
            return;
        if (container != null)
            StashLog.Debug($"Removed container {container.name} from list");
    }

    /// Drops chests whose objects have gone away, which happens across a teleport.
    internal static void Prune()
    {
        foreach (Container container in Containers.ToList())
        {
            if (container == null || container.transform == null || container.GetInventory() == null)
                Containers.Remove(container);
        }
    }

    /// Every target within range of the origin: registered chests, both kinds of
    /// item drawer, and the backpacks the player carries.
    internal static List<IStoreTarget> Nearby(Component origin, float range)
    {
        List<IStoreTarget> targets = new();
        foreach (Container container in Containers)
        {
            if (origin == null || container == null)
                continue;
            float distance = Vector3.Distance(container.transform.position, origin.transform.position);
            if (distance > range)
                continue;
            StashLog.Debug($"Distance to container {container.name} is {distance}m, within the range of {range}m set to store items for this chest");
            targets.Add(new ChestTarget(container));
        }

        Vector3 position = origin.transform.position;
        targets.AddRange(APIs.ItemDrawers_API.AllDrawersInRange(position, range).Select(drawer => new KgDrawerTarget(drawer)));
        targets.AddRange(APIs.MkzItemDrawers_API.AllDrawersInRange(position, range).Select(drawer => new MkzDrawerTarget(drawer)));

        if (BackpacksIsLoaded && DontStoreToBackpacks.Value.IsOff())
        {
            foreach (ItemDrop.ItemData? item in Player.m_localPlayer.GetInventory().GetAllItems())
            {
                ItemContainer? backpack = item?.Data(BackpacksGuid)?.Get<ItemContainer>();
                if (backpack != null)
                    targets.Add(new BackpackTarget(backpack));
            }
        }

        return targets;
    }
}
