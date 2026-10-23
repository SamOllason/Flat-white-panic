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
        var results = Scenario.Orders.Take(5).Select(order => tools.PlaceOrder(order.Id, "oat")).ToArray();
        Assert.Single(results, result => result.StartsWith("Rejected:", StringComparison.Ordinal));
        Assert.Equal(0, tools.Current.OatMilk);
    }

    [Fact]
    public void AllergenCannotBeBypassedBySubstitution()
    {
        var tools = new ShopTools(Scenario.InitialInventory, Scenario.Orders);
        Assert.Contains("severe dairy allergy", tools.PlaceOrder("dee-walkin", "dairy"));
        Assert.Equal(8, tools.Current.DairyMilk);
    }

    [Fact]
    public void UnapprovedSubstitutionIsRejected()
    {
        var tools = new ShopTools(Scenario.InitialInventory, Scenario.Orders);
        Assert.Contains("not approved", tools.PlaceOrder("ava-walkin", "dairy"));
        Assert.Equal(8, tools.Current.DairyMilk);
    }

    [Fact]
    public void BaselineIncludesOnlyCoreInstructions()
    {
        var baseline = ContextMill.Assemble(ContextStrategy.Baseline, Scenario, ContextRoot);
        Assert.Equal(new[] { "core-instructions" }, baseline.Sources.Select(source => source.Id));
    }

    [Fact]
    public void MillSelectsPolicyAndRelevantSourcesButSkipsUnrelatedSources()
    {
        var selected = ContextMill.Assemble(ContextStrategy.MillSelected, Scenario, ContextRoot);
        var sourceIds = selected.Sources.Select(source => source.Id).ToArray();
        Assert.Equal(new[] { "core-instructions", "stock", "recipes", "allergens", "shortage-policy" }, sourceIds);
        Assert.DoesNotContain("loyalty", sourceIds);
        Assert.DoesNotContain("pastry-promotions", sourceIds);
    }

    [Fact]
    public void AllContextContainsSelectedSourcesAndUnrelatedSources()
    {
        var all = ContextMill.Assemble(ContextStrategy.All, Scenario, ContextRoot);
        var selected = ContextMill.Assemble(ContextStrategy.MillSelected, Scenario, ContextRoot);
        Assert.Equal(7, all.Sources.Length);
        Assert.All(selected.Sources, source => Assert.Contains(source.Id, all.Sources.Select(item => item.Id)));
        Assert.True(selected.CharacterCount < all.CharacterCount);
        Assert.Equal(Scenario.InitialInventory, Simulation.CreateScenario().InitialInventory);
    }

    [Fact]
    public async Task DemoTraceRecordsContextAndToolEvents()
    {
        var result = await Simulation.RunAsync(ContextStrategy.MillSelected, false, AppContext.BaseDirectory);
        var json = await File.ReadAllTextAsync(result.TracePath);
        Assert.Contains("contextSelectionReason", json);
        Assert.Contains("shortage-policy", json);
        Assert.Contains("toolCalls", json);
        Assert.Contains("outcome", json);
        Assert.Contains("scripted-demo", json);
    }
}
