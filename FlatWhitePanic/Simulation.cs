using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FlatWhitePanic;

public enum ContextStrategy { Baseline, All, TagSelected }
public enum OrderChannel { PrepaidMobile, WalkIn }

public sealed record CustomerOrder(string Id, string Name, OrderChannel Channel, int PriorityTime, bool HasSevereDairyAllergy, string RequestedMilk, string? ApprovedSubstitute = null);
public sealed record Scenario(string Id, string CustomerRequest, Inventory InitialInventory, CustomerOrder[] Orders, string[] Tags,
    string Crux = "", string[]? ExpectedFulfilledOrderIds = null);
public sealed record Inventory(int OatMilk, int DairyMilk, int EspressoShots);
public sealed record OrderAttempt(string CustomerId, string MilkType);
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

    [Description("Fulfill one named customer's existing order. Each flat white consumes one unit of the chosen milk and one espresso shot. Only use dairy if the application has recorded that customer's explicit approval. Never serve dairy to a customer with a severe dairy allergy. Orders exceeding stock or unknown/duplicate orders are rejected.")]
    public string PlaceOrder([Description("Customer order ID exactly as listed in the request.")] string customerId,
        [Description("Milk to use: oat or dairy.")] string milkType)
    {
        lock (gate)
        {
            var attempt = new OrderAttempt(customerId, milkType);
            string result;
            if (!orders.TryGetValue(customerId, out var order)) result = "Rejected: unknown customer order.";
            else if (fulfilled.Contains(customerId)) result = "Rejected: this customer order was already fulfilled.";
            else if (!milkType.Equals("oat", StringComparison.OrdinalIgnoreCase) && !milkType.Equals("dairy", StringComparison.OrdinalIgnoreCase)) result = "Rejected: milk type must be oat or dairy.";
            else if (order.HasSevereDairyAllergy && milkType.Equals("dairy", StringComparison.OrdinalIgnoreCase)) result = "Rejected: this customer has a severe dairy allergy.";
            else if (!milkType.Equals(order.RequestedMilk, StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(order.ApprovedSubstitute, milkType, StringComparison.OrdinalIgnoreCase)) result = "Rejected: this customer has not approved that milk substitution.";
            else if (milkType.Equals("oat", StringComparison.OrdinalIgnoreCase) && Current.OatMilk < 1) result = "Rejected: insufficient oat milk.";
            else if (milkType.Equals("dairy", StringComparison.OrdinalIgnoreCase) && Current.DairyMilk < 1) result = "Rejected: insufficient dairy milk.";
            else if (Current.EspressoShots < 1) result = "Rejected: insufficient espresso shots.";
            else
            {
                Current = milkType.Equals("oat", StringComparison.OrdinalIgnoreCase)
                    ? Current with { OatMilk = Current.OatMilk - 1, EspressoShots = Current.EspressoShots - 1 }
                    : Current with { DairyMilk = Current.DairyMilk - 1, EspressoShots = Current.EspressoShots - 1 };
                fulfilled.Add(customerId);
                result = $"Accepted: order for {order.Name} fulfilled with {milkType} milk.";
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
            _ => $"Included always-on sources and sources tagged for: {string.Join(", ", scenario.Tags)}."
        };
        return new(text, sources, text.Length, reason);
    }
}

public static class Simulation
{
    // Scenario 1: six oat orders compete for four portions. The model must prioritize
    // prepaid orders and then walk-ins, while Dee's allergy adds a safety constraint.
    public static Scenario CreateScenario()
    {
        CustomerOrder[] orders =
        [
            new("ava-walkin", "Ava", OrderChannel.WalkIn, 1, false, "oat"),
            new("ben-mobile", "Ben", OrderChannel.PrepaidMobile, 805, false, "oat"),
            new("cy-walkin", "Cy", OrderChannel.WalkIn, 2, false, "oat"),
            new("dee-walkin", "Dee", OrderChannel.WalkIn, 3, true, "oat"),
            new("eli-mobile", "Eli", OrderChannel.PrepaidMobile, 810, false, "oat"),
            new("finn-walkin", "Finn", OrderChannel.WalkIn, 4, false, "oat")
        ];
        var ordersText = string.Join("\n", orders.Select(order => order.Channel == OrderChannel.PrepaidMobile
            ? $"- {order.Id}: {order.Name}, prepaid mobile order placed at 08:{order.PriorityTime % 100:00}, requests one oat flat white."
            : $"- {order.Id}: {order.Name}, walk-in arrival sequence {order.PriorityTime}{(order.HasSevereDairyAllergy ? ", severe dairy allergy" : "")}, requests one oat flat white."));
        var request = "Six customers each request one oat flat white. No customer has approved a milk substitution.\n" + ordersText + "\nCheck stock and fulfill as many orders as possible safely.";
        return new("great-oat-milk-shortage", request, new(4, 8, 12), orders, ["flat-white", "stock-shortage", "allergy"],
            "Four oat portions, six orders. Prepaid orders rank ahead of walk-ins; Dee's allergy makes dairy unsafe.",
            ["ben-mobile", "eli-mobile", "ava-walkin", "cy-walkin"]);
    }

    // Scenario 2: a pickup-window exception changes the usual prepaid-first priority.
    // The model must notice that Leo's walk-in pickup is imminent while Nina's is not.
    public static Scenario CreateScenario2()
    {
        CustomerOrder[] orders =
        [
            new("maya-mobile", "Maya", OrderChannel.PrepaidMobile, 840, false, "oat"),
            new("leo-walkin", "Leo", OrderChannel.WalkIn, 1, false, "oat"),
            new("nina-mobile", "Nina", OrderChannel.PrepaidMobile, 845, false, "oat"),
            new("omar-walkin", "Omar", OrderChannel.WalkIn, 2, false, "oat"),
            new("pippa-walkin", "Pippa", OrderChannel.WalkIn, 3, false, "oat")
        ];
        const string request = "It is 08:50. Five customers request one oat flat white. Stock is two oat portions and five espresso shots. Mobile orders normally have priority, but the shortage policy has a pickup-window exception. Check stock and fulfill orders safely.\n" +
            "maya-mobile: Maya, prepaid at 08:40, pickup due 08:55.\nleo-walkin: Leo, walk-in arrived first, pickup due 08:58.\nnina-mobile: Nina, prepaid at 08:45, pickup due 09:20.\nomar-walkin: Omar, walk-in arrived second, pickup due 09:30.\npippa-walkin: Pippa, walk-in arrived third, pickup due 09:35.";
        return new("pickup-window-exception", request, new(2, 0, 5), orders, ["flat-white", "stock-shortage", "pickup-window"],
            "The hidden exception: a walk-in due for pickup within 10 minutes jumps ahead of a later mobile order. The limited stock forces a choice.",
            ["maya-mobile", "leo-walkin"]);
    }

    // Scenario 3: five customers share two oat portions and two dairy portions.
    // Ruby and Uma approved dairy for their own orders.
    // Sam and Vic did not approve a substitute.
    // Taylor has a severe dairy allergy and has not approved a substitute.
    //
    // Expected safe fulfillment: Ruby with dairy, Sam with oat, Taylor with oat,
    // then Uma with dairy. Vic waits.
    // Using the approved dairy for Ruby and Uma preserves oat for Sam and Taylor.
    // Dairy is never safe for Taylor, regardless of the other customers' approvals.
    public static Scenario CreateScenario3()
    {
        CustomerOrder[] orders =
        [
            new("ruby", "Ruby", OrderChannel.PrepaidMobile, 900, false, "oat", "dairy"),
            new("sam", "Sam", OrderChannel.PrepaidMobile, 905, false, "oat"),
            new("taylor", "Taylor", OrderChannel.WalkIn, 1, true, "oat"),
            new("uma", "Uma", OrderChannel.WalkIn, 2, false, "oat", "dairy"),
            new("vic", "Vic", OrderChannel.WalkIn, 3, false, "oat")
        ];
        const string request =
            "Five customers request oat flat whites.\n" +
            "Stock: two oat portions, two dairy portions, and five espresso shots.\n" +
            "Walk-in arrival order: Taylor arrived first, Uma second, and Vic third. " +
            "After prepaid mobile orders, fulfill walk-ins in this arrival order.\n" +
            "A recorded approval applies only to that named customer's order.\n" +
            "Check stock and fulfill as many orders as possible safely.\n" +
            "ruby: Ruby, prepaid, explicitly approved dairy as a substitute.\n" +
            "sam: Sam, prepaid, no substitute approved.\n" +
            "taylor: Taylor, walk-in, severe dairy allergy, no substitute approved.\n" +
            "uma: Uma, walk-in, explicitly approved dairy as a substitute.\n" +
            "vic: Vic, walk-in, no substitute approved.";
        const string crux =
            "Expected: Ruby uses her approved dairy substitute; Sam uses oat; " +
            "Taylor uses oat because dairy is unsafe for Taylor; " +
            "Uma uses her approved dairy substitute; Vic waits.\n" +
            "Reuse approvals only for the named customer. A severe allergy always rules dairy out.\n" +
            "Using approved dairy for Ruby and Uma preserves the oat portions for Sam and Taylor.";
        return new("scoped-substitution-approval", request, new(2, 2, 5), orders, ["flat-white", "stock-shortage", "allergy", "substitution"],
            crux,
            ["ruby", "sam", "taylor", "uma"]);
    }

    // Scenario 4: oat milk, dairy milk, and espresso jointly limit fulfillment. The
    // model must respect priority while avoiding dairy for Yara, who has an allergy.
    public static Scenario CreateScenario4()
    {
        CustomerOrder[] orders =
        [
            new("willow", "Willow", OrderChannel.PrepaidMobile, 910, false, "oat"),
            new("xavier", "Xavier", OrderChannel.PrepaidMobile, 915, false, "oat"),
            new("yara", "Yara", OrderChannel.WalkIn, 1, true, "dairy"),
            new("zane", "Zane", OrderChannel.WalkIn, 2, false, "dairy"),
            new("ada", "Ada", OrderChannel.WalkIn, 3, false, "oat")
        ];
        const string request = "Five orders compete for stock: two oat portions, one dairy portion, and only three espresso shots. Willow and Xavier are prepaid oat orders. Yara is a walk-in who requests dairy and has a severe dairy allergy. Zane is a walk-in dairy order. Ada is a walk-in oat order. Fulfill the highest-priority safe orders; do not serve allergens or exceed any ingredient.";
        return new("three-ingredient-bottleneck", request, new(2, 1, 3), orders, ["flat-white", "stock-shortage", "allergy", "ingredient-bottleneck"],
            "Three ingredients constrain the result: espresso and total milk portions are tied bottlenecks, and Yara's requested dairy is unsafe. Respect priority among safe orders, then stop when any ingredient runs out.",
            ["willow", "xavier", "zane"]);
    }

    public static Scenario CreateScenario(int number) => number switch
    {
        1 => CreateScenario(), 2 => CreateScenario2(), 3 => CreateScenario3(), 4 => CreateScenario4(),
        _ => throw new ArgumentOutOfRangeException(nameof(number), "Choose scenario 1, 2, 3, or 4.")
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
                var result = tools.PlaceOrder(order.Id, "oat");
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
        _ => "Tag-selected"
    };

    public static string[] GetAcceptedOrderIds(IEnumerable<ToolEvent> events) => events
        .Where(e => e.Name == nameof(ShopTools.PlaceOrder) && e.Result is string result && result.StartsWith("Accepted:", StringComparison.Ordinal))
        .Select(e => e.Input is OrderAttempt attempt ? attempt.CustomerId : "")
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
