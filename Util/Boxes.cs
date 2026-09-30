using OttoStash.Patches;
using Backpacks;
using ItemDataManager;

namespace OttoStash.Util;

public class Boxes
{
    // Track containers that have been added to the game. This is used to prevent checking for containers frequently in code.
    internal static readonly List<Container> Containers = new();
    private static readonly List<Container> ContainersToAdd = new();
    private static readonly List<Container> ContainersToRemove = new();
    internal static readonly List<IContainer> ContainersToPing = new();
    internal static bool StoringPaused;

    internal static void AddContainer(Container container)
    {
        if (!Containers.Contains(container))
        {
            ContainersToAdd.Add(container);
            OttoStashLogger.LogDebug($"Added container {container.name} to list");
        }

        UpdateContainers();
    }

    internal static void RemoveContainer(Container container)
    {
        if (Containers.Contains(container))
        {
            ContainersToRemove.Add(container);
            if (container)
                OttoStashLogger.LogDebug($"Removed container {container.name} from list");
        }

        UpdateContainers();
    }

    internal static void UpdateContainers()
    {
        Containers.AddRange(ContainersToAdd);
        ContainersToAdd.Clear();

        foreach (Container container in ContainersToRemove)
        {
            Containers.Remove(container);
        }

        ContainersToRemove.Clear();
    }

    internal static List<IContainer> GetNearbyContainers<T>(T gameObject, float rangeToUse) where T : Component
    {
        List<IContainer> nearbyContainers = new();
        foreach (Container container in Containers)
        {
            if (gameObject == null || container == null) continue;
            float distance = Vector3.Distance(container.transform.position, gameObject.transform.position);
            if (!(distance <= rangeToUse)) continue;
            // log the distance and the range to use
            OttoStashLogger.LogDebug($"Distance to container {container.name} is {distance}m, within the range of {rangeToUse}m set to store items for this chest");
            nearbyContainers.Add(VanillaContainers.Create(container));
        }

        IEnumerable<IContainer> backpacksEnumerable = new List<IContainer>();
        List<IContainer> backpackList = [];
        if (BackpacksIsLoaded && DontStoreToBackpacks.Value.IsOff())
        {
            // Get all backpacks in the player inventory
            foreach (ItemDrop.ItemData? allItem in Player.m_localPlayer.GetInventory().GetAllItems().Where(x => x?.Data(BackpacksGuid)?.Get<ItemContainer>() != null))
            {
                BackpackContainer backpackContainer = BackpackContainer.Create(allItem?.Data(BackpacksGuid)?.Get<ItemContainer>()!);
                if (backpackList.Contains(backpackContainer)) continue;
                backpackList.Add(backpackContainer);
            }

            backpacksEnumerable = backpackList;
        }

        IEnumerable<IContainer> drawers = APIs.ItemDrawers_API.AllDrawersInRange(gameObject.transform.position, rangeToUse).Select(kgDrawer.Create);
        IEnumerable<IContainer> drawersMkz = APIs.MkzItemDrawers_API.AllDrawersInRange(gameObject.transform.position, rangeToUse).Select(mkzDrawer.Create);
        return nearbyContainers.Concat(drawers).Concat(drawersMkz).Concat(backpacksEnumerable).ToList();
    }


    public static void AddContainerIfNotExists(string containerName)
    {
        if (yamlData != null && !yamlData.ContainsKey(containerName))
        {
            yamlData[containerName] = new Dictionary<string, object>
            {
                { "exclude", new List<string>() },
                { "includeOverride", new List<string>() },
            };

            YamlUtils.WriteYaml(yamlPath);
        }
    }

    // Get a list of all excluded prefabs for all containers in the container data

    public static Dictionary<string, List<string?>> GetExcludedPrefabsForAllContainers()
    {
        Dictionary<string, List<string?>> excludedPrefabsForAllContainers = new Dictionary<string, List<string?>>();

        foreach (string? container in GetAllContainers()!)
        {
            excludedPrefabsForAllContainers[container] = GetExcludedPrefabs(container);
        }

        return excludedPrefabsForAllContainers;
    }

    // Get a list of all containers
    public static List<string>? GetAllContainers()
    {
        return yamlData?.Keys.Where(key => key != "groups").ToList();
    }

    // Check if a prefab is excluded from a container

    public static bool CanItemBeStored(string container, string prefab)
    {
        if (yamlData == null)
        {
            OttoStashLogger.LogError("yamlData is null.");
            return false;
        }

        if (!yamlData.ContainsKey(container))
        {
            return true; // Allow storing by default if the container is not defined in yamlData
        }

        Dictionary<object, object>? containerData = yamlData[container] as Dictionary<object, object>;
        if (containerData == null)
        {
            OttoStashLogger.LogError($"Unable to cast containerData for container '{container}' to Dictionary<object, object>.");
            return false;
        }

        List<object>? excludeList = containerData.TryGetValue("exclude", out object? value1)
            ? value1 as List<object>
            : new List<object>();
        List<object>? includeOverrideList = containerData.TryGetValue("includeOverride", out object? value)
            ? value as List<object>
            : new List<object>();

        if (excludeList == null)
        {
            OttoStashLogger.LogError($"Unable to cast excludeList for container '{container}' to List<object>.");
            return false;
        }

        if (includeOverrideList == null)
        {
            OttoStashLogger.LogError($"Unable to cast includeOverrideList for container '{container}' to List<object>.");
            return false;
        }

        if (includeOverrideList.Contains(prefab))
        {
            return true;
        }

        foreach (object? excludedItem in excludeList)
        {
            if (prefab.Equals(excludedItem))
            {
                return false;
            }

            if (GroupUtils.IsGroupDefined((string)excludedItem))
            {
                List<string?>? groupItems = GroupUtils.GetItemsInGroup((string)excludedItem);
                if (groupItems.Contains(prefab))
                {
                    return false;
                }
            }
        }

        return true;
    }


    internal static bool IsPrefabExcluded(string prefab, List<object> exclusionList)
    {
        if (exclusionList != null)
        {
            foreach (object? excludeItem in exclusionList)
            {
                string? excludeItemName = excludeItem.ToString();

                if (groups.TryGetValue(excludeItemName, out HashSet<string?>? groupPrefabs))
                {
                    if (groupPrefabs.Contains(prefab))
                    {
                        return true;
                    }
                }
                else if (excludeItemName == prefab)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static List<string?> GetExcludedPrefabs(string container)
    {
        if (yamlData != null && yamlData.TryGetValue(container, out object containerData))
        {
            Dictionary<object, object>? containerInfo = containerData as Dictionary<object, object>;
            if (containerInfo != null && containerInfo.TryGetValue("exclude", out object excludeData))
            {
                List<object>? excludeList = excludeData as List<object>;
                if (excludeList != null)
                {
                    List<string?> excludedPrefabs = new List<string?>();
                    foreach (object? excludeItem in excludeList)
                    {
                        string? excludeItemName = excludeItem.ToString();
                        if (groups.TryGetValue(excludeItemName, out HashSet<string?>? groupPrefabs))
                        {
                            excludedPrefabs.AddRange(groupPrefabs);
                        }
                        else
                        {
                            excludedPrefabs.Add(excludeItemName);
                        }
                    }

                    return excludedPrefabs;
                }
            }
        }

        return new List<string?>();
    }


    internal static void RPC_RequestPause(long sender, bool pause, Container container)
    {
        if (!container.m_nview.IsValid())
            return;
        if (container.m_nview.IsOwner())
        {
            container.m_nview.GetZDO().Set(ContainerAwakePatch.storingPausedHash, pause);
        }
    }
}