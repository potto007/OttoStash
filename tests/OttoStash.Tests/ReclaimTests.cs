using OttoStash.Reclaiming;

namespace OttoStash.Tests;

/// The Recycle_N_Reclaim settings carry-over: parsing the old file and mapping it
/// onto the new section and key names.
public class ReclaimCarryOverTests
{
    private const string OldConfig = """
        ## Settings file was created by plugin Recycle_N_Reclaim v1.4.4
        ## Plugin GUID: Azumatt.Recycle_N_Reclaim

        [1 - General]

        ## If on, the configuration is locked and can be changed by server admins only.
        # Setting type: Toggle
        # Default value: On
        Lock Configuration = Off

        Apply Crafted By = Off

        [2 - Inventory Recycle]

        Lock to Admin = Off
        DiscardHotkey(s) = Delete + LeftShift
        TrashingModifierKeybind1 = C
        BorderColorTrashedItem = 00FF00FF

        [3 - Reclaiming]

        RecyclingRate = 0.75

        [4 - UI]

        EnableExperimentalCraftingTabUI = Off
        ContainerButtonPosition = {"x":10.0,"y":-20.0,"z":-1.0}

        [zDebug]

        DebugAllowSpammyLogs = On
        """;

    [Fact]
    public void Renamed_settings_land_under_their_new_names()
    {
        Dictionary<(string Section, string Key), string> mapped = ReclaimCarryOver.Map(OldConfig);

        Assert.Equal("Off", mapped[("Reclaim", "ApplyCraftedBy")]);
        Assert.Equal("Off", mapped[("Discard", "LockToAdmin")]);
        Assert.Equal("Delete + LeftShift", mapped[("Discard", "DiscardHotkey")]);
        Assert.Equal("C", mapped[("Trash", "TrashingModifierKeybind")]);
        Assert.Equal("00FF00FF", mapped[("Trash", "BorderColorTrashedSlot")]);
        Assert.Equal("0.75", mapped[("Reclaim", "RecyclingRate")]);
        Assert.Equal("Off", mapped[("ReclaimUI", "ReclaimTabEnabled")]);
        Assert.Equal("""{"x":10.0,"y":-20.0,"z":-1.0}""", mapped[("ReclaimUI", "ContainerButtonPosition")]);
        Assert.Equal("On", mapped[("ReclaimDebug", "DebugAllowSpammyLogs")]);
    }

    [Fact]
    public void The_old_lock_is_not_carried_over()
    {
        Dictionary<(string Section, string Key), string> mapped = ReclaimCarryOver.Map(OldConfig);

        Assert.DoesNotContain(mapped.Keys, key => key.Key.Contains("Lock Configuration") || key.Key == "LockConfiguration");
        Assert.Equal(9, mapped.Count);
    }

    [Fact]
    public void Same_key_in_two_sections_maps_to_two_settings()
    {
        const string config = """
            [2 - Inventory Recycle]
            ReturnEnchantedResources = Off
            [3 - Reclaiming]
            ReturnEnchantedResources = On
            """;

        Dictionary<(string Section, string Key), string> mapped = ReclaimCarryOver.Map(config);

        Assert.Equal("Off", mapped[("Discard", "ReturnEnchantedResources")]);
        Assert.Equal("On", mapped[("Reclaim", "ReturnEnchantedResources")]);
    }

    [Fact]
    public void Every_new_setting_name_is_PascalCase_with_no_ordinal()
    {
        foreach ((string section, string key) in ReclaimCarryOver.Renames.Values)
        {
            Assert.Matches("^[A-Z][A-Za-z0-9]*$", section);
            Assert.Matches("^[A-Z][A-Za-z0-9]*$", key);
        }
    }

    [Fact]
    public void A_config_with_the_reclaim_section_is_not_carried_over_again()
    {
        Assert.True(ReclaimCarryOver.HasReclaimSettings("[1 - General]\r\nX = 1\r\n\r\n[Reclaim]\r\n\r\nRecyclingRate = 0.5\r\n"));
        Assert.False(ReclaimCarryOver.HasReclaimSettings("[1 - General]\nX = 1\n[ReclaimUI]\nY = 2\n"));
    }

    [Fact]
    public void Windows_line_endings_and_a_byte_order_mark_parse()
    {
        Dictionary<(string Section, string Key), string> values = ReclaimCarryOver.ParseCfg("﻿[3 - Reclaiming]\r\nRecyclingRate = 0.25\r\n");

        Assert.Equal("0.25", values[("3 - Reclaiming", "RecyclingRate")]);
    }
}

/// The reclaim rules file: exclusions, include overrides, groups and rate overrides.
public class ReclaimRulesTests
{
    private const string Rules = """
        groups:
          Tier 2 Items:
            - Bronze
            - PickaxeBronze
        inventory:
          exclude:
            - Tier 2 Items
          includeOverride:
            - Bronze
        reclaiming:
          exclude:
            - Hammer
          recycleRates:
            SwordBronze: 1.0
            Tier 2 Items: 0.25
            Greedy: 3.0
        containers:
          piece_chest:
            exclude:
              - Tier 2 Items
        """;

    public ReclaimRulesTests()
    {
        ReclaimRules.Read(Rules);
    }

    [Fact]
    public void An_item_in_an_excluded_group_is_excluded()
    {
        Assert.True(ReclaimRules.IsExcludedInInventory("PickaxeBronze"));
    }

    [Fact]
    public void An_include_override_beats_its_group_exclusion()
    {
        Assert.False(ReclaimRules.IsExcludedInInventory("Bronze"));
    }

    [Fact]
    public void A_directly_excluded_item_is_excluded_from_reclaiming()
    {
        Assert.True(ReclaimRules.IsExcludedInReclaiming("Hammer"));
        Assert.False(ReclaimRules.IsExcludedInReclaiming("PickaxeBronze"));
    }

    [Fact]
    public void A_container_the_file_does_not_name_excludes_nothing()
    {
        Assert.True(ReclaimRules.IsExcludedInContainer("piece_chest", "Bronze"));
        Assert.False(ReclaimRules.IsExcludedInContainer("piece_chest_wood", "Bronze"));
    }

    [Fact]
    public void Rate_overrides_match_by_item_then_by_group_and_clamp()
    {
        Assert.Equal(1.0f, ReclaimRules.RecycleRateOverride("SwordBronze"));
        Assert.Equal(0.25f, ReclaimRules.RecycleRateOverride("PickaxeBronze"));
        Assert.Equal(1.0f, ReclaimRules.RecycleRateOverride("Greedy"));
        Assert.Null(ReclaimRules.RecycleRateOverride("Wood"));
    }

    [Fact]
    public void Missing_sections_read_as_empty()
    {
        ReclaimRules.Read("groups:\n  Mine:\n    - Wood\n");

        Assert.False(ReclaimRules.IsExcludedInReclaiming("Wood"));
        Assert.False(ReclaimRules.IsExcludedInInventory("Wood"));
        Assert.Null(ReclaimRules.RecycleRateOverride("Wood"));
    }
}

/// The yield arithmetic.
public class ReclaimMathTests
{
    [Fact]
    public void The_yield_rounds_down()
    {
        Assert.Equal((2, false), ReclaimMath.FinalAmount(5, 1.0, 0.5f, unstackable: false, unstackableReturnsAtLeastOne: true));
    }

    [Fact]
    public void An_unstackable_item_returns_at_least_one_when_asked()
    {
        Assert.Equal((1, false), ReclaimMath.FinalAmount(1, 1.0, 0.5f, unstackable: true, unstackableReturnsAtLeastOne: true));
        Assert.Equal((0, false), ReclaimMath.FinalAmount(1, 1.0, 0.5f, unstackable: true, unstackableReturnsAtLeastOne: false));
        Assert.Equal((0, false), ReclaimMath.FinalAmount(1, 1.0, 0.5f, unstackable: false, unstackableReturnsAtLeastOne: true));
    }

    [Fact]
    public void A_resource_the_recipe_never_cost_is_flagged()
    {
        Assert.Equal((0, true), ReclaimMath.FinalAmount(0, 1.0, 1f, unstackable: true, unstackableReturnsAtLeastOne: true));
    }

    [Fact]
    public void A_partial_stack_returns_its_share()
    {
        // 10 arrows from a recipe that crafts 20, costing 8 wood, at full rate.
        Assert.Equal((4, false), ReclaimMath.FinalAmount(8, 10 / 20.0, 1f, unstackable: false, unstackableReturnsAtLeastOne: true));
    }

    [Fact]
    public void Spent_quality_levels_stop_at_the_item_maximum()
    {
        Assert.Equal(new[] { 1, 2, 3 }, ReclaimMath.SpentQualityLevels(false, maxQuality: 4, quality: 3));
        Assert.Equal(new[] { 1, 2, 3, 4 }, ReclaimMath.SpentQualityLevels(false, maxQuality: 4, quality: 6));
    }

    [Fact]
    public void Upgrader_resources_count_only_levels_past_the_maximum()
    {
        Assert.Empty(ReclaimMath.SpentQualityLevels(true, maxQuality: 4, quality: 3));
        Assert.Equal(new[] { 5, 6 }, ReclaimMath.SpentQualityLevels(true, maxQuality: 4, quality: 6));
    }
}
