using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FlatWhitePanic;

public enum ContextStrategy { Baseline, All, Curated }
public sealed record CustomerOrder(string Id, string Name, string PlacedAt, string PickupTime);
public sealed record Scenario(string Id, string CustomerRequest, Inventory InitialInventory, CustomerOrder[] Orders, string[] Tags,
    string Crux = "", string[]? ExpectedFulfilledOrderIds = null);
public sealed record Inventory(int OatMilk, int EspressoShots);
public sealed record OrderAttempt(string CustomerId);
public sealed record ToolEvent(string Name, object Input, object Result);
public sealed record RunResult(string ConsoleTrace, string TracePath);
public sealed record ContextSource(string Id, string Path, string Version, string[] Tags, bool AlwaysInclude = false);
public sealed record ContextManifest(string Version, ContextSource[] Sources);
public sealed record ContextPack(string Text, ContextSource[] Sources, int CharacterCount, string SelectionReason);

public sealed class ShopTools(Inventory inventory, CustomerOrder[] customers)
{
    private readonly object gate = new();
    private readonly Dictionary<string, CustomerOrder> orders = customers.ToDictionary(order => order.Id, StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> fulfilled = new(StringComparer.OrdinalIgnoreCase);
    public Inventory Current { get; private set; } = inventory;
    public List<ToolEvent> Events { get; } = [];

    [Description("Read current coffee shop stock. Does not change inventory.")]
    public string GetInventory()
    {
        lock (gate)
        {
            var snapshot = Current;
            Events.Add(new(nameof(GetInventory), new { }, snapshot));
            return JsonSerializer.Serialize(snapshot);
        }
    }

    [Description("Fulfill one named customer's existing oat flat white order. Each drink consumes one oat portion and one espresso shot. Orders exceeding stock or unknown/duplicate orders are rejected.")]
    public string PlaceOrder([Description("Customer order ID exactly as listed in the request.")] string customerId)
    {
        lock (gate)
        {
            var attempt = new OrderAttempt(customerId);
            string result;
            if (!orders.TryGetValue(customerId, out var order)) result = "Rejected: unknown customer order.";
            else if (fulfilled.Contains(customerId)) result = "Rejected: this customer order was already fulfilled.";
            else if (Current.OatMilk < 1) result = "Rejected: insufficient oat portions.";
            else if (Current.EspressoShots < 1) result = "Rejected: insufficient espresso shots.";
            else
            {
                Current = Current with { OatMilk = Current.OatMilk - 1, EspressoShots = Current.EspressoShots - 1 };
                fulfilled.Add(customerId);
                result = $"Accepted: order for {order.Name} fulfilled.";
            }
            Events.Add(new(nameof(PlaceOrder), attempt, result));
            return result;
        }
    }
}

public static class ContextAssembler
{
    public static ContextPack Assemble(ContextStrategy strategy, Scenario scenario, string contextRoot)
    {
        var manifestPath = Path.Combine(contextRoot, "manifest.json");
        var manifest = JsonSerializer.Deserialize<ContextManifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Could not read context manifest: {manifestPath}");

        var sources = strategy switch
        {
            ContextStrategy.Baseline => manifest.Sources.Where(source => source.AlwaysInclude).ToArray(),
            ContextStrategy.All => manifest.Sources,
            _ => manifest.Sources.Where(source => source.AlwaysInclude || source.Tags.Intersect(scenario.Tags, StringComparer.OrdinalIgnoreCase).Any()).ToArray()
        };

        var contents = sources.Select(source =>
        {
            var fullPath = Path.GetFullPath(Path.Combine(contextRoot, source.Path));
            var root = Path.GetFullPath(contextRoot) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Context source escapes context directory: {source.Path}");
            return $"## {source.Id} (v{source.Version})\n{File.ReadAllText(fullPath)}";
        });
        var text = string.Join("\n\n", contents);
        var reason = strategy switch
        {
            ContextStrategy.Baseline => "Only always-on core instructions were included.",
            ContextStrategy.All => "All manifest sources were included.",
            _ => $"Curated core instructions and manifest sources marked for: {string.Join(", ", scenario.Tags)}."
        };
        return new(text, sources, text.Length, reason);
    }
}

public static class Simulation
{
    // Scenario 1 establishes the basic rule: order time and pickup time point to different customers.
    public static Scenario CreateScenario()
    {
        CustomerOrder[] orders =
        [
            new("ava", "Ava", "08:45", "09:15"),
            new("ben", "Ben", "08:50", "09:00")
        ];
        const string request = "Two customers each ordered one oat flat white. There is one oat portion and two espresso shots in stock. " +
            "Ava placed her order at 08:45 for pickup at 09:15. Ben placed his order at 08:50 for pickup at 09:00. " +
            "Check stock and decide which order the shop should fulfill first. Explain who must wait.";
        return new("pickup-promise-shortage", request, new(1, 2), orders, ["flat-white", "stock-shortage"],
            "Only one oat flat white can be made. Ava ordered first, but Ben was promised the earlier pickup. The shortage policy prioritizes the earliest promised pickup, so Ben gets the drink and Ava waits.",
            ["ben"]);
    }

    // Scenario 2 mirrors Scenario 1 by changing only the promised pickup times. It checks that
    // the model follows the pickup-time policy whichever customer's name it favors, rather than
    // memorizing that Ben was the expected winner in Scenario 1.
    public static Scenario CreateScenario2()
    {
        CustomerOrder[] orders =
        [
            new("ava", "Ava", "08:45", "09:00"),
            new("ben", "Ben", "08:50", "09:15")
        ];
        const string request = "Two customers each ordered one oat flat white. There is one oat portion and two espresso shots in stock. " +
            "Ava placed her order at 08:45 for pickup at 09:00. Ben placed his order at 08:50 for pickup at 09:15. " +
            "Check stock and decide which order the shop should fulfill first. Explain who must wait.";
        return new("pickup-promise-mirror", request, new(1, 2), orders, ["flat-white", "stock-shortage"],
            "This mirrors Scenario 1: the pickup times are swapped, so Ava now has the earlier promised pickup and should get the one oat flat white. Ben waits.",
            ["ava"]);
    }

    // Scenario 3 adds one realistic tie-break only when pickup promises are equal. It tests
    // whether context can supply the reserved-slot rule when order time alone favors Ava.
    public static Scenario CreateScenario3()
    {
        CustomerOrder[] orders =
        [
            new("ava", "Ava", "08:45", "09:00"),
            new("ben", "Ben", "08:50", "09:00")
        ];
        const string request = "Two customers each need one oat flat white. There is one oat portion and two espresso shots in stock. " +
            "Ava is a walk-in who joined the queue at 08:45 and is waiting at the counter. Ben reserved the 09:00 pickup slot online at 08:50. " +
            "Both orders have a promised pickup time of 09:00. Check stock, decide which order to fulfill first, and explain who must wait.";
        return new("pickup-promise-tie-break", request, new(1, 2), orders, ["flat-white", "stock-shortage"],
            "Both orders have the same promised pickup time, so the shop's tie-break applies. A reserved pickup slot outranks the walk-in queue; Ben gets the one oat flat white and Ava waits.",
            ["ben"]);
    }

    public static Scenario CreateScenario(int number) => number switch
    {
        1 => CreateScenario(),
        2 => CreateScenario2(),
        3 => CreateScenario3(),
        _ => throw new ArgumentOutOfRangeException(nameof(number), "Choose scenario 1, 2, or 3.")
    };

    public static async Task<RunResult> RunAsync(ContextStrategy strategy, bool live, string baseDirectory)
        => await RunAsync(strategy, live, baseDirectory, CreateScenario());

    public static async Task<RunResult> RunAsync(ContextStrategy strategy, bool live, string baseDirectory, Scenario scenario)
    {
        var tools = new ShopTools(scenario.InitialInventory, scenario.Orders);
        var context = ContextAssembler.Assemble(strategy, scenario, Path.Combine(baseDirectory, "context"));
        var stopwatch = Stopwatch.StartNew();
        string response;
        var model = "scripted-demo";
        string? error = null;

        if (live)
        {
            var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT");
            var deployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_DEPLOYMENT");
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
                throw new InvalidOperationException("Live mode needs FOUNDRY_PROJECT_ENDPOINT and FOUNDRY_MODEL_DEPLOYMENT. No credentials are read from files.");
            model = deployment;
            var agent = new AIProjectClient(new Uri(endpoint), new AzureDeveloperCliCredential()).AsAIAgent(
                model: deployment, instructions: context.Text, name: "FlatWhitePanicManager",
                tools: [AIFunctionFactory.Create(tools.GetInventory), AIFunctionFactory.Create(tools.PlaceOrder)]);
            response = (await agent.RunAsync(scenario.CustomerRequest)).ToString();
        }
        else
        {
            // This fixed FIFO script exercises the harness; its behavior is not evidence about LLMs.
            tools.GetInventory();
            var accepted = new List<string>();
            foreach (var order in scenario.Orders.Take(scenario.InitialInventory.OatMilk))
            {
                var result = tools.PlaceOrder(order.Id);
                if (result.StartsWith("Accepted:", StringComparison.Ordinal)) accepted.Add(order.Id);
            }
            response = $"Scripted decision: fulfilled {string.Join(", ", accepted)} in request-list order. The script does not read or follow the shortage policy.";
        }

        stopwatch.Stop();
        var acceptedIds = GetAcceptedOrderIds(tools.Events);
        var fulfilledNames = scenario.Orders.Where(order => acceptedIds.Contains(order.Id, StringComparer.OrdinalIgnoreCase)).Select(order => order.Name).ToArray();
        var trace = new
        {
            runId = Guid.NewGuid(), timestampUtc = DateTimeOffset.UtcNow,
            scenarioId = scenario.Id, strategy = strategy.ToString(),
            customerOrders = scenario.Orders,
            contextSources = context.Sources.Select(source => new { source.Id, source.Path, source.Version }),
            contextSelectionReason = context.SelectionReason,
            contextCharacterCount = context.CharacterCount,
            assembledContext = context.Text, modelIdentifier = model, runMode = live ? "model" : "scripted-demo",
            agentResponse = response, toolCalls = tools.Events,
            validations = tools.Events.Where(e => e.Name == nameof(ShopTools.PlaceOrder)).Select(e => new { input = e.Input, result = e.Result }),
            stateChanges = new { initial = scenario.InitialInventory, final = tools.Current }, error,
            latencyMs = stopwatch.ElapsedMilliseconds, tokenUsage = (object?)null,
            outcome = new { fulfilledOrderIds = acceptedIds, fulfilledCustomers = fulfilledNames, finalInventory = tools.Current }
        };
        var tracePath = Path.Combine(Environment.CurrentDirectory, "traces", $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{strategy.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(tracePath)!);
        await File.WriteAllTextAsync(tracePath, JsonSerializer.Serialize(trace, new JsonSerializerOptions { WriteIndented = true }));

        var readable = $"Flat White Panic — {DisplayName(strategy)} context ({(live ? "LIVE MODEL" : "SCRIPTED DEMO")})\nScenario: {scenario.Id}\nThe crux: {scenario.Crux}\nContext sources: {string.Join(", ", context.Sources.Select(source => $"{source.Id} v{source.Version}"))}\nContext size: {context.CharacterCount} characters\nSelection: {context.SelectionReason}\nAgent response: {response}\nFulfilled customers: {string.Join(", ", fulfilledNames)}\nInventory: {JsonSerializer.Serialize(scenario.InitialInventory)} → {JsonSerializer.Serialize(tools.Current)}\nLatency: {stopwatch.ElapsedMilliseconds} ms";
        return new(readable, tracePath);
    }

    public static string DisplayName(ContextStrategy strategy) => strategy switch
    {
        ContextStrategy.Baseline => "Baseline",
        ContextStrategy.All => "All",
        _ => "Curated"
    };

    public static string[] GetAcceptedOrderIds(IEnumerable<ToolEvent> events) => events
        .Where(e => e.Name == nameof(ShopTools.PlaceOrder) && e.Result is string result && result.StartsWith("Accepted:", StringComparison.Ordinal))
        .Select(e => e.Input is OrderAttempt attempt ? attempt.CustomerId : "")
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
