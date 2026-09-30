using OttoStash.Util;

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
        piece_chest:
          exclude: []
          includeOverride: []
        """;

    public ContainerRulesTests()
    {
        YamlUtils.ReadYaml(Rules);
        YamlUtils.ParseGroups();
    }

    [Fact]
    public void An_item_in_an_excluded_group_is_refused()
    {
        Assert.False(Boxes.CanItemBeStored("piece_chest_wood", "Wood"));
    }

    [Fact]
    public void A_directly_excluded_item_is_refused()
    {
        Assert.False(Boxes.CanItemBeStored("piece_chest_wood", "Stone"));
    }

    [Fact]
    public void An_include_override_wins_over_its_group_exclusion()
    {
        Assert.True(Boxes.CanItemBeStored("piece_chest_wood", "FineWood"));
    }

    [Fact]
    public void An_item_no_rule_mentions_is_allowed()
    {
        Assert.True(Boxes.CanItemBeStored("piece_chest_wood", "Flint"));
    }

    [Fact]
    public void A_chest_with_empty_rules_takes_anything()
    {
        Assert.True(Boxes.CanItemBeStored("piece_chest", "Wood"));
    }

    [Fact]
    public void A_chest_the_rules_do_not_mention_takes_anything()
    {
        Assert.True(Boxes.CanItemBeStored("piece_chest_blackmetal", "Wood"));
    }

    [Fact]
    public void Group_membership_is_read_from_the_rules()
    {
        Assert.True(GroupUtils.IsGroupDefined("Woods"));
        Assert.Equal(new[] { "Wood", "FineWood" }, GroupUtils.GetItemsInGroup("Woods"));
    }
}
