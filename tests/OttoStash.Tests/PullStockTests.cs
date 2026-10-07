using OttoStash.Pulling;
using OttoStash.Storing;

namespace OttoStash.Tests;

/// A source with fixed counts that records what was taken.
internal sealed class FakeSource : IPullSource
{
    private readonly Dictionary<string, int> _counts;

    internal FakeSource(string ruleName, Dictionary<string, int> counts)
    {
        RuleName = ruleName;
        _counts = counts;
    }

    public string RuleName { get; }
    public Inventory? Inventory => null;

    public int Count(string sharedName, int quality)
    {
        return _counts.TryGetValue(sharedName, out int count) ? count : 0;
    }

    public int Take(string sharedName, int quality, int amount)
    {
        int taken = Math.Min(amount, Count(sharedName, quality));
        _counts[sharedName] = Count(sharedName, quality) - taken;
        return taken;
    }

    internal int Left(string sharedName) => Count(sharedName, -1);
}

/// Counting and taking across sources, by the rules and Leave One.
[Collection(RulesCollection.Name)]
public class PullStockTests
{
    public PullStockTests()
    {
        ContainerRules.Read("""
            groups: {}
            piece_chest_private:
              pull:
                exclude:
                  - Wood
            """);
        ContainerRules.ParseGroups();
    }

    private static FakeSource Chest(string name, int wood) => new(name, new Dictionary<string, int> { ["$item_wood"] = wood });

    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(5, false, 5)]
    [InlineData(5, true, 4)]
    [InlineData(1, true, 0)]
    [InlineData(0, true, 0)]
    public void Leave_one_keeps_the_last_item(int count, bool leaveOne, int takeable)
    {
        Assert.Equal(takeable, PullStock.Takeable(count, leaveOne));
    }

    [Fact]
    public void Available_skips_a_source_whose_rules_refuse_the_item()
    {
        IPullSource[] sources = [Chest("piece_chest", 3), Chest("piece_chest_private", 10)];
        Assert.Equal(3, PullStock.Available(sources, "$item_wood", "Wood", -1, "", leaveOne: false));
    }

    [Fact]
    public void Available_leaves_one_in_every_source()
    {
        IPullSource[] sources = [Chest("piece_chest", 3), Chest("piece_chest_wood", 2)];
        Assert.Equal(3, PullStock.Available(sources, "$item_wood", "Wood", -1, "", leaveOne: true));
    }

    [Fact]
    public void Take_drains_sources_in_order_and_stops_at_the_amount()
    {
        FakeSource first = Chest("piece_chest", 3);
        FakeSource second = Chest("piece_chest_wood", 10);
        FakeSource third = Chest("piece_chest_wood", 10);

        int taken = PullStock.Take([first, second, third], "$item_wood", "Wood", -1, "", leaveOne: false, amount: 7);

        Assert.Equal(7, taken);
        Assert.Equal(0, first.Left("$item_wood"));
        Assert.Equal(6, second.Left("$item_wood"));
        Assert.Equal(10, third.Left("$item_wood"));
    }

    [Fact]
    public void Take_never_touches_a_refused_source_or_the_last_item()
    {
        FakeSource refused = Chest("piece_chest_private", 10);
        FakeSource open = Chest("piece_chest", 4);

        int taken = PullStock.Take([refused, open], "$item_wood", "Wood", -1, "", leaveOne: true, amount: 10);

        Assert.Equal(3, taken);
        Assert.Equal(10, refused.Left("$item_wood"));
        Assert.Equal(1, open.Left("$item_wood"));
    }
}
