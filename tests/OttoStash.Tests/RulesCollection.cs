namespace OttoStash.Tests;

/// The rule table is static, so the classes that load rules into it run one at a time.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RulesCollection
{
    internal const string Name = "Container rules";
}
