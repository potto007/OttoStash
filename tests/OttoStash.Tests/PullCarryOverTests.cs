using OttoStash.Pulling;
using OttoStash.Storing;

namespace OttoStash.Tests;

/// Carrying AzuCraftyBoxes rules into the OttoStash rules file.
[Collection(RulesCollection.Name)]
public class PullCarryOverTests
{
    private const string Otto = """
        # OttoStash rules
        groups:
          BronzeGear:
            - ArmorBronzeChest

        piece_chest:
          range: 10 # store range
          exclude:
            - Stone
        """;

    private const string Crafty = """
        groups:
          Arrows:
            - ArrowBronze
            - ArrowFlint
          BronzeGear:
            - Bronze
        piece_chest:
          exclude:
            - Arrows
          includeOverride:
            - ArrowFlint
        piece_chest_wood:
          exclude: []
          includeOverride: []
        piece_workbench:
          exclude:
            - Wood
        """;

    private static string MergeAndLoad(List<string> notes)
    {
        string merged = PullCarryOver.Merge(Otto, Crafty, notes)!;
        ContainerRules.Read(merged);
        ContainerRules.ParseGroups();
        return merged;
    }

    [Fact]
    public void A_shared_container_keeps_its_store_rules_and_gains_a_pull_block()
    {
        MergeAndLoad(new List<string>());

        Assert.False(ContainerRules.CanStore("piece_chest", "Stone"));
        Assert.Equal(10f, ContainerRules.RangeFor("piece_chest", 5f));
        Assert.False(ContainerRules.CanPull("piece_chest", "ArrowBronze", ""));
        Assert.True(ContainerRules.CanPull("piece_chest", "ArrowFlint", ""));
        Assert.True(ContainerRules.CanStore("piece_chest", "ArrowBronze"));
    }

    [Fact]
    public void A_container_only_AzuCraftyBoxes_knew_is_appended()
    {
        MergeAndLoad(new List<string>());

        Assert.False(ContainerRules.CanPull("piece_chest_blackmetal", "Wood", "piece_workbench"));
        Assert.True(ContainerRules.CanStore("piece_workbench", "Wood"));
    }

    [Fact]
    public void An_entry_with_empty_lists_is_not_carried()
    {
        string merged = MergeAndLoad(new List<string>());

        Assert.DoesNotContain("piece_chest_wood", merged);
    }

    [Fact]
    public void A_group_both_files_define_keeps_the_OttoStash_members()
    {
        List<string> notes = new();
        MergeAndLoad(notes);

        Assert.Equal(new[] { "ArmorBronzeChest" }, ContainerRules.ItemsInGroup("BronzeGear"));
        Assert.Equal(new[] { "ArrowBronze", "ArrowFlint" }, ContainerRules.ItemsInGroup("Arrows"));
        Assert.Contains(notes, note => note.Contains("BronzeGear"));
    }

    [Fact]
    public void The_comments_of_the_OttoStash_file_survive()
    {
        string merged = MergeAndLoad(new List<string>());

        Assert.StartsWith("# OttoStash rules\n", merged);
        Assert.Contains("range: 10 # store range", merged);
    }

    [Fact]
    public void A_merged_file_is_never_merged_again()
    {
        string merged = PullCarryOver.Merge(Otto, Crafty, new List<string>())!;

        Assert.Null(PullCarryOver.Merge(merged, Crafty, new List<string>()));
    }

    [Fact]
    public void The_block_indent_of_the_file_is_kept()
    {
        const string fourSpaces = "piece_chest:\n    range: 10\n";
        string merged = PullCarryOver.Merge(fourSpaces, "piece_chest:\n  exclude:\n    - Wood\n", new List<string>())!;
        ContainerRules.Read(merged);
        ContainerRules.Groups = new();

        Assert.Equal(10f, ContainerRules.RangeFor("piece_chest", 5f));
        Assert.False(ContainerRules.CanPull("piece_chest", "Wood", ""));
    }

    [Fact]
    public void A_file_without_groups_gets_the_AzuCraftyBoxes_groups()
    {
        string merged = PullCarryOver.Merge("piece_chest:\n  range: 3\n", Crafty, new List<string>())!;
        ContainerRules.Read(merged);
        ContainerRules.ParseGroups();

        Assert.Equal(new[] { "Bronze" }, ContainerRules.ItemsInGroup("BronzeGear"));
    }
}
