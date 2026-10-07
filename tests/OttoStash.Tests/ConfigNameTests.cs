using OttoStash.Configuration;

namespace OttoStash.Tests;

/// Moving a player's .cfg from the spaced setting names to PascalCase.
public class ConfigNameTests
{
    private static string[] Lines(string text) => text.Replace("\r\n", "\n").Split('\n');

    [Theory]
    [InlineData("1 - General", "General")]
    [InlineData("1.5 - Fish", "Fish")]
    [InlineData("2 - Shortcuts", "Shortcuts")]
    [InlineData("3 - Favoriting", "Favoriting")]
    [InlineData("4 - Armor Stands", "ArmorStands")]
    [InlineData("CraftFromChests", "CraftFromChests")]
    [InlineData("ReclaimUI", "ReclaimUI")]
    public void Section_drops_the_ordinal_and_the_spaces(string name, string expected)
    {
        Assert.Equal(expected, ConfigName.Section(name));
    }

    [Theory]
    [InlineData("Lock Configuration", "LockConfiguration")]
    [InlineData("Dont Store to Backpacks", "DontStoreToBackpacks")]
    [InlineData("Ping VFX", "PingVFX")]
    [InlineData("Store Single Item Shortcut", "StoreSingleItemShortcut")]
    [InlineData("IntervalSeconds", "IntervalSeconds")]
    public void Key_drops_the_spaces(string name, string expected)
    {
        Assert.Equal(expected, ConfigName.Key(name));
    }

    [Fact]
    public void Rewrite_renames_sections_and_keys_and_keeps_values_and_comments()
    {
        string[] old = Lines("""
            [1 - General]

            ## If on, the configuration is locked.
            # Setting type: Toggle
            Lock Configuration = Off

            Ping VFX = vfx_Potion_health_medium
            IntervalSeconds = 2.5

            [1.5 - Fish]

            Fish Suction = On
            """);

        string[]? migrated = ConfigNameMigration.Rewrite(old, null);

        Assert.NotNull(migrated);
        Assert.Equal(Lines("""
            [General]

            ## If on, the configuration is locked.
            # Setting type: Toggle
            LockConfiguration = Off

            PingVFX = vfx_Potion_health_medium
            IntervalSeconds = 2.5

            [Fish]

            FishSuction = On
            """), migrated);
    }

    [Fact]
    public void Rewrite_leaves_a_migrated_file_alone()
    {
        string[] current = Lines("""
            [General]
            LockConfiguration = Off

            [CraftFromChests]
            PullRange = 20
            """);

        Assert.Null(ConfigNameMigration.Rewrite(current, null));
    }

    [Fact]
    public void Rewrite_keeps_the_spaced_value_when_a_file_has_both_spellings()
    {
        // 3.2.0 wrote [General] PlayerRange, then 3.2.1 went back to the spaced name and
        // BepInEx kept the line it no longer bound. The spaced line is the newer value.
        string[] mixed = Lines("""
            [1 - General]
            Player Range = 12
            IntervalSeconds = 4

            [General]
            PlayerRange = 7
            IntervalSeconds = 10
            """);

        string[]? migrated = ConfigNameMigration.Rewrite(mixed, null);

        Assert.NotNull(migrated);
        Assert.Equal(Lines("""
            [General]
            PlayerRange = 12
            IntervalSeconds = 4

            [General]
            """), migrated);
    }
}
