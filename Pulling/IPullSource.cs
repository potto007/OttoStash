namespace OttoStash.Pulling;

/// Anything nearby that crafting and building can take materials out of: a
/// chest, a drawer, a backpack or a gem bag.
internal interface IPullSource
{
    /// The prefab name the pull rules know this source by.
    string RuleName { get; }

    /// The items inside, for callers that need the item instances. Null for a
    /// source that has no inventory of its own, such as a drawer.
    Inventory? Inventory { get; }

    /// How many of an item the source holds. A negative quality counts every quality.
    int Count(string sharedName, int quality);

    /// Takes up to the given amount out of the source and returns how many it took.
    int Take(string sharedName, int quality, int amount);
}
