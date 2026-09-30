namespace OttoStash.Storing;

/// Builds the item groups that need no rules file: food, potions, weapons by
/// skill, armor by slot, ores, metals, woods, and so on, from the game's own
/// item list and crafting stations.
internal static class PredefinedGroups
{
    private static readonly string[] BossTrophies = ["eikthyr", "elder", "bonemass", "dragonqueen", "goblinking", "SeekerQueen"];

    internal static void Build(ObjectDB objectDb)
    {
        foreach (GameObject prefab in objectDb.m_items.Where(x => x.GetComponentInChildren<ItemDrop>() != null))
        {
            ItemDrop itemDrop = prefab.GetComponentInChildren<ItemDrop>();
            if (itemDrop.m_itemData == null || itemDrop.m_itemData.m_shared == null)
                continue;

            GameObject? itemPrefab = objectDb.GetItemPrefab(PrefabNames.FromSceneName(prefab.name));
            // Every drop prefab is pointed at the item itself.
            itemDrop.m_itemData.m_dropPrefab = itemDrop.gameObject;
            if (itemPrefab == null)
                continue;

            ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
            foreach (string groupName in GroupsFor(shared, objectDb))
            {
                if (string.IsNullOrEmpty(groupName))
                    continue;
                StashLog.BuildDebug($"(PredefinedGroups) Adding {itemDrop.m_itemData.m_dropPrefab.name} to {groupName}");
                Add(groupName, itemDrop);
            }

            Add("All", itemDrop);
        }
    }

    private static List<string> GroupsFor(ItemDrop.ItemData.SharedData shared, ObjectDB objectDb)
    {
        List<string> groups = new();

        if (shared.m_food > 0.0 && shared.m_foodStamina > 0.0)
            groups.Add("Food");

        if (shared.m_consumeStatusEffect != null)
        {
            if ((shared.m_food > 0.0 && shared.m_foodStamina == 0.0)
                || shared.m_consumeStatusEffect.name.ToLower().Contains("potion")
                || shared.m_consumeStatusEffect.m_name.ToLower().Contains("potion")
                || shared.m_consumeStatusEffect.m_category.ToLower().Contains("potion")
                || shared.m_ammoType.ToLower().Contains("mead"))
            {
                groups.Add("Potion");
            }
            else if (shared.m_itemType == ItemDrop.ItemData.ItemType.Fish)
            {
                groups.Add("Fish");
            }
        }

        switch (shared.m_itemType)
        {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeapon
                or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft or ItemDrop.ItemData.ItemType.Bow:
                string? weaponGroup = WeaponGroupFor(shared.m_skillType);
                if (weaponGroup != null)
                {
                    groups.Add(weaponGroup);
                    groups.Add("Weapons");
                }
                break;
            case ItemDrop.ItemData.ItemType.Shield:
                groups.Add("Shield");
                groups.Add(shared.m_timedBlockBonus > 0.0 ? "Round Shield" : "Tower Shield");
                break;
            case ItemDrop.ItemData.ItemType.Helmet:
                groups.AddRange(["Armor", "Helmet"]);
                break;
            case ItemDrop.ItemData.ItemType.Chest:
                groups.AddRange(["Armor", "Chest"]);
                break;
            case ItemDrop.ItemData.ItemType.Legs:
                groups.AddRange(["Armor", "Legs"]);
                break;
            case ItemDrop.ItemData.ItemType.Shoulder:
                groups.AddRange(["Armor", "Shoulder"]);
                break;
            case ItemDrop.ItemData.ItemType.Utility:
                groups.Add("Utility");
                break;
            case ItemDrop.ItemData.ItemType.Trinket:
                groups.Add("Trinket");
                break;
            case ItemDrop.ItemData.ItemType.Ammo:
                if (shared.m_ammoType == "$ammo_bolts")
                    groups.Add("Bolts");
                else if (shared.m_ammoType == "$ammo_arrows")
                    groups.Add("Arrows");
                groups.Add("Ammo");
                break;
            case ItemDrop.ItemData.ItemType.Torch or ItemDrop.ItemData.ItemType.Tool:
                groups.Add("Equipment");
                break;
            case ItemDrop.ItemData.ItemType.Trophy:
                groups.Add(BossTrophies.Any(shared.m_name.EndsWith) ? "Boss Trophy" : "Trophy");
                break;
            case ItemDrop.ItemData.ItemType.Material:
                groups.AddRange(MaterialGroupsFor(shared, objectDb));
                break;
        }

        return groups;
    }

    // The weapon types above also matched the Equipment case in an earlier switch,
    // but a switch takes its first match, so they never reached it. Torch and Tool
    // are the two that did.
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
            Skills.SkillType.ElementalMagic => "ElementalMagic",
            Skills.SkillType.BloodMagic => "BloodMagic",
            _ => null,
        };
    }

    private static List<string> MaterialGroupsFor(ItemDrop.ItemData.SharedData shared, ObjectDB objectDb)
    {
        List<string> groups = new();

        // A material that a cultivator piece needs on its own is a seed, or a crop
        // when the grown plant yields more than one.
        Piece.Requirement[]? PlantRequirements(GameObject piece) => piece.GetComponent<Piece>().m_resources;
        GameObject? cultivatorPiece = objectDb.GetItemPrefab("Cultivator").GetComponent<ItemDrop>().m_itemData.m_shared.m_buildPieces.m_pieces
            .FirstOrDefault(piece => PlantRequirements(piece) is { Length: 1 } requirements && requirements[0].m_resItem.m_itemData.m_shared.m_name == shared.m_name);
        if (cultivatorPiece != null)
        {
            bool yieldsMany = cultivatorPiece.GetComponent<Plant>()?.m_grownPrefabs[0].GetComponent<Pickable>()?.m_amount > 1;
            groups.Add(yieldsMany ? "Crops" : "Seeds");
        }

        if (Converts("smelter", from: shared.m_name) || Converts("blastfurnace", from: shared.m_name))
            groups.Add("Ores");

        if (Converts("smelter", to: shared.m_name) || Converts("blastfurnace", to: shared.m_name))
            groups.Add("Metals");

        if (Converts("charcoal_kiln", from: shared.m_name) || shared.m_name == "$item_elderbark")
            groups.Add("Woods");

        return groups;
    }

    private static bool Converts(string stationPrefab, string? from = null, string? to = null)
    {
        Smelter smelter = ZNetScene.instance.GetPrefab(stationPrefab).GetComponent<Smelter>();
        return smelter.m_conversion.Any(conversion =>
            (from != null && conversion.m_from.m_itemData.m_shared.m_name == from)
            || (to != null && conversion.m_to.m_itemData.m_shared.m_name == to));
    }

    private static void Add(string groupName, ItemDrop itemDrop)
    {
        if (!ContainerRules.GroupExists(groupName))
            ContainerRules.Groups[groupName] = new HashSet<string?>();

        string? prefabName = Utils.GetPrefabName(itemDrop.m_itemData.m_dropPrefab);
        if (ContainerRules.Groups[groupName].Contains(prefabName))
            return;

        ContainerRules.Groups[groupName].Add(prefabName);
        StashLog.Debug($"(PredefinedGroups) Added {prefabName} to {groupName}");
    }
}
