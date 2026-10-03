using System.Text;
using ItemType = ItemDrop.ItemData.ItemType;

namespace OttoStash.Stands;

/// What an armor stand slot takes. The rules are vanilla's, from ArmorStand.UseItem,
/// split so the type rule can be checked without the engine.
internal static class StandRules
{
    /// A slot with no listed types takes anything. Otherwise the item's attach
    /// override, or failing that its type, must be listed.
    internal static bool TypeFits(IReadOnlyList<ItemType> supported, ItemType itemType, ItemType attachOverride)
    {
        if (supported.Count == 0)
            return true;
        ItemType effective = attachOverride != ItemType.None ? attachOverride : itemType;
        return supported.Contains(effective);
    }

    /// Chest and leg pieces are drawn on the stand's body. Everything else hangs
    /// off an attach point, so the prefab must have one.
    internal static bool NeedsAttachPoint(ItemType itemType)
    {
        return itemType != ItemType.Legs && itemType != ItemType.Chest;
    }

    internal static bool HasAttachPoint(GameObject? dropPrefab)
    {
        if (dropPrefab == null)
            return false;
        Transform root = dropPrefab.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            string name = root.GetChild(i).gameObject.name;
            if (name == "attach" || name == "attach_skin")
                return true;
        }
        return false;
    }

    internal static bool Accepts(ArmorStand.ArmorStandSlot slot, ItemDrop.ItemData item)
    {
        if (!TypeFits(slot.m_supportedTypes, item.m_shared.m_itemType, item.m_shared.m_attachOverride))
            return false;
        return !NeedsAttachPoint(item.m_shared.m_itemType) || HasAttachPoint(item.m_dropPrefab);
    }

    /// "Helmet", "One handed weapon / Shield": the slot's types as a tooltip line.
    internal static string Label(IReadOnlyList<ItemType> supported)
    {
        if (supported.Count == 0)
            return "Any item";
        return string.Join(" / ", supported.Select(Humanize));
    }

    private static string Humanize(ItemType type)
    {
        string name = type.ToString().Replace('_', ' ');
        StringBuilder text = new(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c))
            {
                if (name[i - 1] != ' ')
                    text.Append(' ');
                text.Append(char.ToLowerInvariant(c));
            }
            else
            {
                text.Append(c);
            }
        }
        return text.ToString();
    }
}
