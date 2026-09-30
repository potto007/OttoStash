namespace OttoStash.Storing;

/// The per-chest rules and item groups read from the YAML file. They live here
/// rather than on the plugin so the rule logic can run without BepInEx.
internal static class ContainerRules
{
    /// The parsed YAML: one entry per container prefab plus the "groups" entry.
    internal static Dictionary<string, object>? Rules;

    /// Group name to the prefab names in it, from the YAML and the predefined groups.
    internal static Dictionary<string?, HashSet<string?>> Groups = null!;
}
