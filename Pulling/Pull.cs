namespace OttoStash.Pulling;

/// The engine side of pulling: whether it runs right now, the station the
/// player works at, and the counts the menus read many times a frame.
internal static class Pull
{
    /// Mods that also pay crafting costs out of chests. With one of them loaded,
    /// both would take the same materials, so OttoStash leaves pulling to it.
    internal static readonly string[] ConflictingMods = ["Azumatt.AzuCraftyBoxes", "aedenthorn.CraftFromContainers", "CFCMod"];

    internal static string? ConflictingMod;

    /// The prefab name of the crafting station the local player is using, or empty.
    internal static string Station = "";

    private static readonly Dictionary<(string, int), int> AvailableThisFrame = new();
    private static int _countedFrame = -1;

    internal static bool Active(Player? player)
    {
        return player != null
               && player == Player.m_localPlayer
               && PullEnabled.Value.IsOn()
               && ConflictingMod == null
               && PullSwitch.IsAllowed(player);
    }

    internal static void FindConflictingMod()
    {
        ConflictingMod = ConflictingMods.FirstOrDefault(Chainloader.PluginInfos.ContainsKey);
        if (ConflictingMod != null)
            StashLog.Warning($"{ConflictingMod} is installed and already takes crafting materials from chests, so OttoStash will not. Remove it to use OttoStash's crafting from chests.");
    }

    /// How many of an item the nearby sources may give, at one quality or at any
    /// quality when the quality is negative. Not counting what the player carries.
    internal static int Available(Player player, string sharedName, string prefab, int quality)
    {
        int frame = Time.frameCount;
        if (frame != _countedFrame)
        {
            _countedFrame = frame;
            AvailableThisFrame.Clear();
        }

        if (AvailableThisFrame.TryGetValue((sharedName, quality), out int cached))
            return cached;

        int available = PullStock.Available(Sources(player), sharedName, prefab, quality, Station, LeaveOneItem.Value.IsOn());
        AvailableThisFrame[(sharedName, quality)] = available;
        return available;
    }

    internal static int Take(Player player, string sharedName, string prefab, int quality, int amount)
    {
        if (amount <= 0)
            return 0;

        int taken = PullStock.Take(Sources(player), sharedName, prefab, quality, Station, LeaveOneItem.Value.IsOn(), amount);
        AvailableThisFrame.Clear();
        if (taken < amount)
            StashLog.Debug($"Pulled only {taken} of the {amount} {sharedName} needed from nearby containers.");
        return taken;
    }

    internal static IReadOnlyList<IPullSource> Sources(Player player)
    {
        return PullSources.Near(player, PullRange.Value);
    }

    /// The prefab name the pull rules use for a requirement's item.
    internal static string PrefabOf(Piece.Requirement requirement)
    {
        return PrefabNames.FromSceneName(requirement.m_resItem.gameObject.name);
    }

    /// Whether vanilla charges this requirement at the given station. Upgrade
    /// materials only count at an upgrader station, and only there.
    internal static bool Applies(Piece.Requirement requirement, CraftingStation? station)
    {
        return requirement.m_resItem != null
               && requirement.m_resItem.m_itemData?.m_shared != null
               && requirement.m_upgraderResource == (station != null && station.m_upgrader);
    }
}
