namespace OttoStash.Util;

public class GroupUtils
{
    // Get a list of all excluded ContainerRules.Groups for a container
    public static List<string> GetExcludedGroups(string container)
    {
        if (ContainerRules.Rules != null && ContainerRules.Rules.TryGetValue(container, out object containerData))
        {
            Dictionary<object, object>? containerInfo = containerData as Dictionary<object, object>;
            if (containerInfo != null && containerInfo.TryGetValue("exclude", out object excludeData))
            {
                List<object>? excludeList = excludeData as List<object>;
                if (excludeList != null)
                {
                    return excludeList.Where(excludeItem =>
                            ContainerRules.Groups.ContainsKey(excludeItem.ToString()))
                        .Select(excludeItem => excludeItem.ToString()).ToList();
                }
            }
        }

        return [];
    }

    public static bool IsGroupDefined(string? groupName)
    {
        if (ContainerRules.Rules == null)
        {
            Functions.LogError("The container rules are not loaded, so no group can be checked.");
            return false;
        }

        bool groupInYaml = false;

        if (ContainerRules.Rules.ContainsKey("groups"))
        {
            Dictionary<object, object>? groupsData = ContainerRules.Rules["groups"] as Dictionary<object, object>;
            if (groupsData != null)
            {
                if (groupName != null) groupInYaml = groupsData.ContainsKey(groupName);
            }
            else
            {
                Functions.LogError("Unable to cast groupsData to Dictionary<object, object>.");
            }
        }

        // Check for the group in both ContainerRules.Rules and predefined ContainerRules.Groups
        return groupInYaml || ContainerRules.Groups.ContainsKey(groupName);
    }


// Check if a group exists in the container data
    public static bool GroupExists(string? groupName)
    {
        return ContainerRules.Groups.ContainsKey(groupName);
    }

// Get a list of all ContainerRules.Groups in the container data
    public static List<string?> GetAllGroups()
    {
        return ContainerRules.Groups.Keys.ToList();
    }

// Get a list of all items in a group
    public static List<string?> GetItemsInGroup(string? groupName)
    {
        if (ContainerRules.Groups.TryGetValue(groupName, out HashSet<string?> groupPrefabs))
        {
            return groupPrefabs.ToList();
        }

        return [];
    }

    /*public static bool IsItemInGroup(string itemName, string groupName)
    {
        if (PredefinedGroups.ContainsKey(groupName))
        {
            return PredefinedGroups[groupName].Items.Contains(itemName);
        }

        return false;
    }*/
}