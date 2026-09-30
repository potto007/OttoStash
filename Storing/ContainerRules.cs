using YamlDotNet.Serialization;

namespace OttoStash.Storing;

/// The per-chest rules and item groups read from the YAML file. They live here
/// rather than on the plugin so the rule logic can run without BepInEx.
internal static class ContainerRules
{
    private const string GroupsKey = "groups";
    private const string ExcludeKey = "exclude";
    private const string IncludeOverrideKey = "includeOverride";
    private const string RangeKey = "range";

    /// The parsed YAML: one entry per container prefab plus the "groups" entry.
    internal static Dictionary<string, object>? Rules;

    /// Group name to the prefab names in it, from the YAML and the predefined groups.
    internal static Dictionary<string?, HashSet<string?>> Groups = null!;

    internal static void Read(string yaml)
    {
        IDeserializer deserializer = new DeserializerBuilder().Build();
        Rules = deserializer.Deserialize<Dictionary<string, object>>(yaml);
        StashLog.BuildDebug($"Container rules:\n{yaml}");
    }

    /// Copies the "groups" entry of the rules into the group table. The
    /// predefined groups built from the game's item list are added separately.
    internal static void ParseGroups()
    {
        Groups ??= new Dictionary<string?, HashSet<string?>>();

        if (Rules == null)
        {
            StashLog.Error("The container rules are not loaded.");
            return;
        }

        if (!Rules.TryGetValue(GroupsKey, out object groupData))
        {
            StashLog.Error("The container rules have no groups entry.");
            return;
        }

        if (groupData is not Dictionary<object, object> groupTable)
        {
            StashLog.Error("The groups entry of the container rules is not a table.");
            return;
        }

        foreach (KeyValuePair<object, object> group in groupTable)
        {
            string? groupName = group.Key?.ToString();
            if (groupName == null || group.Value is not List<object> prefabs)
                continue;

            HashSet<string?> prefabNames = new();
            foreach (object prefab in prefabs)
            {
                string? prefabName = prefab?.ToString();
                if (prefabName != null)
                    prefabNames.Add(prefabName);
            }

            Groups[groupName] = prefabNames;
        }
    }

    /// Whether a prefab may go into a container, by the container's exclude list
    /// and the include overrides that beat it. A container the rules do not
    /// mention takes anything.
    internal static bool CanStore(string container, string prefab)
    {
        if (Rules == null)
        {
            StashLog.Error("The container rules are not loaded.");
            return false;
        }

        if (!Rules.TryGetValue(container, out object entry))
            return true;

        if (entry is not Dictionary<object, object> containerData)
        {
            StashLog.Error($"The rules for container '{container}' are not a table.");
            return false;
        }

        List<object>? excludeList = containerData.TryGetValue(ExcludeKey, out object? exclude) ? exclude as List<object> : new List<object>();
        List<object>? includeOverrideList = containerData.TryGetValue(IncludeOverrideKey, out object? includeOverride) ? includeOverride as List<object> : new List<object>();

        if (excludeList == null)
        {
            StashLog.Error($"The exclude entry for container '{container}' is not a list.");
            return false;
        }

        if (includeOverrideList == null)
        {
            StashLog.Error($"The includeOverride entry for container '{container}' is not a list.");
            return false;
        }

        if (includeOverrideList.Contains(prefab))
            return true;

        foreach (object? excluded in excludeList)
        {
            if (prefab.Equals(excluded))
                return false;

            string excludedName = (string)excluded;
            if (IsGroupDefined(excludedName) && ItemsInGroup(excludedName).Contains(prefab))
                return false;
        }

        return true;
    }

    /// The pickup range for a container prefab, or the fallback when the rules
    /// give none. Negative when the rules are not loaded or malformed.
    internal static float RangeFor(string container, float fallback)
    {
        if (Rules == null)
        {
            StashLog.Error("The container rules are not loaded, so no container range can be read.");
            return -1f;
        }

        if (!Rules.TryGetValue(container, out object entry))
            return fallback;

        if (entry is not Dictionary<object, object> containerData)
        {
            StashLog.Error($"The rules for container '{container}' are not a table.");
            return -1f;
        }

        if (containerData.TryGetValue(RangeKey, out object rangeValue) && float.TryParse(rangeValue.ToString(), out float range))
            return range;

        return fallback;
    }

    /// True for a group named in the rules file or built from the game's items.
    internal static bool IsGroupDefined(string? groupName)
    {
        if (Rules == null)
        {
            StashLog.Error("The container rules are not loaded, so no group can be checked.");
            return false;
        }

        bool inRules = false;
        if (Rules.TryGetValue(GroupsKey, out object groupData))
        {
            if (groupData is Dictionary<object, object> groupTable)
                inRules = groupName != null && groupTable.ContainsKey(groupName);
            else
                StashLog.Error("The groups entry of the container rules is not a table.");
        }

        return inRules || Groups.ContainsKey(groupName);
    }

    internal static bool GroupExists(string? groupName)
    {
        return Groups.ContainsKey(groupName);
    }

    internal static List<string?> ItemsInGroup(string? groupName)
    {
        return Groups.TryGetValue(groupName, out HashSet<string?> prefabs) ? prefabs.ToList() : new List<string?>();
    }
}
