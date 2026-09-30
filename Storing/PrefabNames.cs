namespace OttoStash.Storing;

internal static class PrefabNames
{
    private static readonly char[] SceneSuffixStart = ['(', ' '];

    /// The prefab name behind a scene object's name, which the engine suffixes
    /// with "(Clone)" or an instance number.
    internal static string FromSceneName(string sceneName)
    {
        int suffix = sceneName.IndexOfAny(SceneSuffixStart);
        return suffix < 0 ? sceneName : sceneName.Substring(0, suffix);
    }

    /// The item's drop prefab name, or its shared name when the prefab is not set.
    internal static string Of(ItemDrop.ItemData item)
    {
        return item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
    }
}
