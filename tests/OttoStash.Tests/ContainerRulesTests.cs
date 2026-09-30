using OttoStash.Storing;

namespace OttoStash.Tests;

/// The per-chest YAML rules, parsed by the mod's own reader.
public class ContainerRulesTests
{
    private const string Rules = """
        groups:
          Woods:
            - Wood
            - FineWood
        piece_chest_wood:
          exclude:
            - Woods
            - Stone
          includeOverride:
            - FineWood
          range: 7.5
        piece_chest:
          exclude: []
          includeOverride: []
        """;

    public ContainerRulesTests()
    {
        ContainerRules.Read(Rules);
        ContainerRules.ParseGroups();
    }

    [Fact]
    public void An_item_in_an_excluded_group_is_refused()
    {
        Assert.False(ContainerRules.CanStore("piece_chest_wood", "Wood"));
    }

    [Fact]
    public void A_directly_excluded_item_is_refused()
    {
        Assert.False(ContainerRules.CanStore("piece_chest_wood", "Stone"));
    }

    [Fact]
    public void An_include_override_wins_over_its_group_exclusion()
    {
        Assert.True(ContainerRules.CanStore("piece_chest_wood", "FineWood"));
    }

    [Fact]
    public void An_item_no_rule_mentions_is_allowed()
    {
        Assert.True(ContainerRules.CanStore("piece_chest_wood", "Flint"));
    }

    [Fact]
    public void A_chest_with_empty_rules_takes_anything()
    {
        Assert.True(ContainerRules.CanStore("piece_chest", "Wood"));
    }

    [Fact]
    public void A_chest_the_rules_do_not_mention_takes_anything()
    {
        Assert.True(ContainerRules.CanStore("piece_chest_blackmetal", "Wood"));
    }

    [Fact]
    public void Group_membership_is_read_from_the_rules()
    {
        Assert.True(ContainerRules.IsGroupDefined("Woods"));
        Assert.Equal(new[] { "Wood", "FineWood" }, ContainerRules.ItemsInGroup("Woods"));
    }

    [Fact]
    public void A_range_in_the_rules_wins_over_the_fallback()
    {
        Assert.Equal(7.5f, ContainerRules.RangeFor("piece_chest_wood", fallback: 10f));
    }

    [Theory]
    [InlineData("piece_chest")]
    [InlineData("piece_chest_blackmetal")]
    public void A_chest_without_a_range_uses_the_fallback(string container)
    {
        Assert.Equal(10f, ContainerRules.RangeFor(container, fallback: 10f));
    }
}
