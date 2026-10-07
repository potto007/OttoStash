using OttoStash.Storing;

namespace OttoStash.Tests;

/// The pull blocks of the per-chest rules, which decide what crafting may take.
[Collection(RulesCollection.Name)]
public class PullRulesTests
{
    private const string Rules = """
        groups:
          Woods:
            - Wood
            - FineWood
        piece_chest_wood:
          exclude:
            - Stone
          pull:
            exclude:
              - Woods
            includeOverride:
              - FineWood
        piece_workbench:
          pull:
            exclude:
              - Flint
        piece_chest:
          range: 5
        """;

    public PullRulesTests()
    {
        ContainerRules.Read(Rules);
        ContainerRules.ParseGroups();
    }

    [Fact]
    public void A_pull_exclude_keeps_a_group_in_the_container()
    {
        Assert.False(ContainerRules.CanPull("piece_chest_wood", "Wood", ""));
    }

    [Fact]
    public void A_pull_include_override_beats_its_group()
    {
        Assert.True(ContainerRules.CanPull("piece_chest_wood", "FineWood", ""));
    }

    [Fact]
    public void A_store_exclude_does_not_stop_pulling()
    {
        Assert.True(ContainerRules.CanPull("piece_chest_wood", "Stone", ""));
    }

    [Fact]
    public void A_pull_exclude_does_not_stop_storing()
    {
        Assert.True(ContainerRules.CanStore("piece_chest_wood", "Wood"));
    }

    [Fact]
    public void The_station_rules_apply_to_every_container()
    {
        Assert.False(ContainerRules.CanPull("piece_chest", "Flint", "piece_workbench"));
        Assert.True(ContainerRules.CanPull("piece_chest", "Flint", "forge"));
    }

    [Theory]
    [InlineData("piece_chest")]
    [InlineData("piece_chest_blackmetal")]
    public void A_container_without_a_pull_block_gives_anything(string container)
    {
        Assert.True(ContainerRules.CanPull(container, "Wood", ""));
    }
}
