using YamlDotNet.Serialization;

namespace OttoStash.Reclaiming;

/// potto007.OttoStash.Reclaim.yml, the format Recycle_N_Reclaim's exclude lists
/// used, so a carried-over file reads unchanged.
public class ReclaimRulesFile
{
    [YamlMember(Alias = "groups")] public Dictionary<string, List<string>>? Groups { get; set; }
    [YamlMember(Alias = "containers")] public Dictionary<string, ExcludeList>? Containers { get; set; }
    [YamlMember(Alias = "reclaiming")] public ReclaimingRules? Reclaiming { get; set; }
    [YamlMember(Alias = "inventory")] public ExcludeList? Inventory { get; set; }
}

public class ExcludeList
{
    [YamlMember(Alias = "exclude")] public List<string>? Exclude { get; set; }
    [YamlMember(Alias = "includeOverride")] public List<string>? IncludeOverride { get; set; }
}

public class ReclaimingRules : ExcludeList
{
    [YamlMember(Alias = "recycleRates")] public Dictionary<string, float>? RecycleRates { get; set; }
}

/// What may be reclaimed or discarded, by item prefab name and by group. The
/// predefined groups are Recycle_N_Reclaim's own, which are not the same set as
/// the storing groups (Helmets, Ammunition, Utilities, Tools and so on), so a
/// carried-over file keeps its meaning.
internal static class ReclaimRules
{
    private static ReclaimRulesFile _file = Normalize(new ReclaimRulesFile());
    private static Dictionary<string, HashSet<string>> _groups = new();
    internal static readonly Dictionary<string, HashSet<string>> PredefinedGroups = new();

    internal static void Read(string yaml)
    {
        IDeserializer deserializer = new DeserializerBuilder().Build();
        _file = Normalize(deserializer.Deserialize<ReclaimRulesFile?>(yaml) ?? new ReclaimRulesFile());
        RebuildGroups();
        StashLog.BuildDebug($"Reclaim rules:\n{yaml}");
    }

    private static ReclaimRulesFile Normalize(ReclaimRulesFile file)
    {
        file.Groups ??= new Dictionary<string, List<string>>();
        file.Containers ??= new Dictionary<string, ExcludeList>();
        file.Reclaiming ??= new ReclaimingRules();
        file.Inventory ??= new ExcludeList();
        return file;
    }

    /// The file's groups with the predefined ones laid over them. A predefined
    /// group replaces a file group of the same name, as it did before.
    internal static void RebuildGroups()
    {
        Dictionary<string, HashSet<string>> groups = new();
        foreach (KeyValuePair<string, List<string>> group in _file.Groups!)
            groups[group.Key] = new HashSet<string>(group.Value ?? new List<string>());
        foreach (KeyValuePair<string, HashSet<string>> group in PredefinedGroups)
            groups[group.Key] = group.Value;
        _groups = groups;
    }

    internal static float? RecycleRateOverride(string prefabName)
    {
        Dictionary<string, float>? rates = _file.Reclaiming!.RecycleRates;
        if (rates == null || rates.Count == 0)
            return null;

        if (rates.TryGetValue(prefabName, out float rate))
            return Mathf.Clamp01(rate);

        foreach (KeyValuePair<string, float> entry in rates)
        {
            if (_groups.TryGetValue(entry.Key, out HashSet<string> group) && group.Contains(prefabName))
                return Mathf.Clamp01(entry.Value);
        }

        return null;
    }

    internal static bool IsExcludedInReclaiming(string prefabName) => IsExcluded(_file.Reclaiming!, prefabName);

    internal static bool IsExcludedInInventory(string prefabName) => IsExcluded(_file.Inventory!, prefabName);

    /// A container the file does not name excludes nothing.
    internal static bool IsExcludedInContainer(string containerName, string prefabName)
    {
        return _file.Containers!.TryGetValue(containerName, out ExcludeList? container) && container != null && IsExcluded(container, prefabName);
    }

    /// An include override, by name or by group, beats any exclusion.
    private static bool IsExcluded(ExcludeList list, string prefabName)
    {
        if (list.IncludeOverride != null && list.IncludeOverride.Any(entry => Matches(entry, prefabName)))
            return false;

        return list.Exclude != null && list.Exclude.Any(entry => Matches(entry, prefabName));
    }

    private static bool Matches(string entry, string prefabName)
    {
        return entry == prefabName || (_groups.TryGetValue(entry, out HashSet<string> group) && group.Contains(prefabName));
    }

    internal static void AddToPredefinedGroup(string groupName, string prefabName)
    {
        if (!PredefinedGroups.TryGetValue(groupName, out HashSet<string> group))
        {
            group = new HashSet<string>();
            PredefinedGroups[groupName] = group;
        }

        group.Add(prefabName);
    }

    internal static void WriteExampleFile(string path)
    {
        const string resourceName = "OttoStash.ReclaimExample.yml";
        using Stream? resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (resource == null)
            throw new FileNotFoundException($"Resource '{resourceName}' not found in the assembly.");
        using StreamReader reader = new(resource);
        File.WriteAllText(path, reader.ReadToEnd());
    }
}

/// Builds the reclaim predefined groups from the game's item list.
internal static class ReclaimGroups
{
    private static readonly string[] BossTrophies = ["eikthyr", "elder", "bonemass", "dragonqueen", "goblinking", "SeekerQueen"];

    internal static void Build(ObjectDB objectDb)
    {
        ReclaimRules.PredefinedGroups.Clear();
        foreach (GameObject prefab in objectDb.m_items)
        {
            ItemDrop? itemDrop = prefab != null ? prefab.GetComponentInChildren<ItemDrop>() : null;
            if (itemDrop == null || itemDrop.m_itemData?.m_shared == null)
                continue;
            if (objectDb.GetItemPrefab(PrefabNames.FromSceneName(prefab!.name)) == null)
                continue;

            string prefabName = global::Utils.GetPrefabName(itemDrop.gameObject);
            string? groupName = GroupFor(itemDrop.m_itemData.m_shared, objectDb);
            if (!string.IsNullOrEmpty(groupName))
                ReclaimRules.AddToPredefinedGroup(groupName!, prefabName);
            ReclaimRules.AddToPredefinedGroup("All", prefabName);
        }

        ReclaimRules.RebuildGroups();
    }

    private static string? GroupFor(ItemDrop.ItemData.SharedData shared, ObjectDB objectDb)
    {
        string? groupName = null;
        if (shared.m_food > 0.0 && shared.m_foodStamina > 0.0)
            groupName = "Food";

        if (shared.m_food > 0.0 && shared.m_foodStamina == 0.0)
            groupName = "Potion";
        else if (shared.m_itemType == ItemDrop.ItemData.ItemType.Fish)
            groupName = "Fish";

        switch (shared.m_itemType)
        {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
                or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow:
                return WeaponGroupFor(shared.m_skillType) ?? groupName;
            case ItemDrop.ItemData.ItemType.Torch:
                return "Equipment";
            case ItemDrop.ItemData.ItemType.Trophy:
                return BossTrophies.Any(shared.m_name.EndsWith) ? "Boss Trophy" : "Trophy";
            case ItemDrop.ItemData.ItemType.Material:
                return MaterialGroupFor(shared, objectDb) ?? groupName;
            case ItemDrop.ItemData.ItemType.Helmet:
                return "Helmets";
            case ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Shoulder
                or ItemDrop.ItemData.ItemType.Legs or ItemDrop.ItemData.ItemType.Hands:
                return "Armor";
            case ItemDrop.ItemData.ItemType.Ammo or ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                return "Ammunition";
            case ItemDrop.ItemData.ItemType.Utility:
                return "Utilities";
            case ItemDrop.ItemData.ItemType.Tool:
                return "Tools";
            case ItemDrop.ItemData.ItemType.Misc:
                return "Miscellaneous";
            case ItemDrop.ItemData.ItemType.Customization:
                return "Customizations";
        }

        return groupName;
    }

    private static string? WeaponGroupFor(Skills.SkillType skill)
    {
        return skill switch
        {
            Skills.SkillType.Swords => "Swords",
            Skills.SkillType.Bows => "Bows",
            Skills.SkillType.Crossbows => "Crossbows",
            Skills.SkillType.Axes => "Axes",
            Skills.SkillType.Clubs => "Clubs",
            Skills.SkillType.Knives => "Knives",
            Skills.SkillType.Pickaxes => "Pickaxes",
            Skills.SkillType.Polearms => "Polearms",
            Skills.SkillType.Spears => "Spears",
            _ => null,
        };
    }

    /// Later checks win, in the order Recycle_N_Reclaim applied them.
    private static string? MaterialGroupFor(ItemDrop.ItemData.SharedData shared, ObjectDB objectDb)
    {
        string? groupName = null;
        GameObject? cultivator = objectDb.GetItemPrefab("Cultivator");
        GameObject? seedPiece = cultivator?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces?.m_pieces.FirstOrDefault(piece =>
        {
            Piece.Requirement[]? requirements = piece.GetComponent<Piece>()?.m_resources;
            return requirements is { Length: 1 } && requirements[0].m_resItem?.m_itemData.m_shared.m_name == shared.m_name;
        });
        if (seedPiece != null)
            groupName = seedPiece.GetComponent<Plant>()?.m_grownPrefabs[0].GetComponent<Pickable>()?.m_amount > 1 ? "Crops" : "Seeds";

        foreach (string station in new[] { "smelter", "blastfurnace" })
        {
            if (Converts(station, from: shared.m_name))
                groupName = "Ores";
            if (Converts(station, to: shared.m_name))
                groupName = "Metals";
        }

        if (Converts("charcoal_kiln", from: shared.m_name) || shared.m_name == "$item_elderbark")
            groupName = "Woods";

        return groupName;
    }

    private static bool Converts(string stationPrefab, string? from = null, string? to = null)
    {
        Smelter? smelter = ZNetScene.instance.GetPrefab(stationPrefab)?.GetComponent<Smelter>();
        return smelter != null && smelter.m_conversion.Any(conversion =>
            (from != null && conversion.m_from.m_itemData.m_shared.m_name == from)
            || (to != null && conversion.m_to.m_itemData.m_shared.m_name == to));
    }
}
