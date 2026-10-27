using FlatWhitePanic;
using Xunit;

namespace FlatWhitePanic.Tests;

public class SimulationTests
{
    private static string ContextRoot => Path.Combine(AppContext.BaseDirectory, "context");
    private static Scenario Scenario => Simulation.CreateScenario();

    [Fact]
    public void InventoryCannotBecomeNegative()
    {
        var tools = new ShopTools(Scenario.InitialInventory, Scenario.Orders);
        var results = Scenario.Orders.Take(5).Select(order => tools.PlaceOrder(order.Id)).ToArray();
        Assert.Single(results, result => result.StartsWith("Rejected:", StringComparison.Ordinal));
        Assert.Equal(0, tools.Current.OatMilk);
    }

    [Fact]
    public void BaselineIncludesOnlyCoreInstructions()
    {
        var baseline = ContextAssembler.Assemble(ContextStrategy.Baseline, Scenario, ContextRoot);
        Assert.Equal(new[] { "core-instructions" }, baseline.Sources.Select(source => source.Id));
    }

    [Fact]
    public void TagSelectionIncludesRelevantSourcesAndSkipsUnrelatedSources()
    {
        var selected = ContextAssembler.Assemble(ContextStrategy.Curated, Scenario, ContextRoot);
        var sourceIds = selected.Sources.Select(source => source.Id).ToArray();
        Assert.Equal(new[] { "core-instructions", "stock", "recipes", "shortage-policy" }, sourceIds);
        Assert.DoesNotContain("loyalty", sourceIds);
        Assert.DoesNotContain("pastry-promotions", sourceIds);
    }

    [Fact]
    public void AllContextContainsSelectedSourcesAndUnrelatedSources()
    {
        var all = ContextAssembler.Assemble(ContextStrategy.All, Scenario, ContextRoot);
        var selected = ContextAssembler.Assemble(ContextStrategy.Curated, Scenario, ContextRoot);
        Assert.Equal(6, all.Sources.Length);
        Assert.All(selected.Sources, source => Assert.Contains(source.Id, all.Sources.Select(item => item.Id)));
        Assert.True(selected.CharacterCount < all.CharacterCount);
        Assert.Equal(Scenario.InitialInventory, Simulation.CreateScenario().InitialInventory);
    }

    [Fact]
    public async Task DemoTraceRecordsContextAndToolEvents()
    {
        var result = await Simulation.RunAsync(ContextStrategy.Curated, false, AppContext.BaseDirectory);
        var json = await File.ReadAllTextAsync(result.TracePath);
        Assert.Contains("contextSelectionReason", json);
        Assert.Contains("shortage-policy", json);
        Assert.Contains("toolCalls", json);
        Assert.Contains("outcome", json);
        Assert.Contains("scripted-demo", json);
    }
}
