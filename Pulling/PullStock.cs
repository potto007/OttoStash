namespace OttoStash.Pulling;

/// Counting and taking materials across a set of sources, by the pull rules and
/// the leave-one setting. It touches nothing but the sources it is given, so it
/// runs without the engine.
internal static class PullStock
{
    /// What a source may give of the count it holds. Leave One keeps the last
    /// item of each kind in every source, so the chest still has it to store into.
    internal static int Takeable(int count, bool leaveOne)
    {
        if (count <= 0)
            return 0;
        return leaveOne ? count - 1 : count;
    }

    internal static int Available(IReadOnlyList<IPullSource> sources, string sharedName, string prefab, int quality, string station, bool leaveOne)
    {
        int total = 0;
        foreach (IPullSource source in sources)
        {
            if (!ContainerRules.CanPull(source.RuleName, prefab, station))
                continue;
            total += Takeable(source.Count(sharedName, quality), leaveOne);
        }

        return total;
    }

    /// Takes up to the amount, source by source in the order given, and returns
    /// how many were taken in all.
    internal static int Take(IReadOnlyList<IPullSource> sources, string sharedName, string prefab, int quality, string station, bool leaveOne, int amount)
    {
        int taken = 0;
        foreach (IPullSource source in sources)
        {
            if (taken >= amount)
                break;
            if (!ContainerRules.CanPull(source.RuleName, prefab, station))
                continue;

            int want = Math.Min(amount - taken, Takeable(source.Count(sharedName, quality), leaveOne));
            if (want > 0)
                taken += source.Take(sharedName, quality, want);
        }

        return taken;
    }
}
