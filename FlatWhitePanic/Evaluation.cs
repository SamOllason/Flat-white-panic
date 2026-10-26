using System.Text.Json;
using System.Text;

namespace FlatWhitePanic;

public static class Evaluation
{
    private static readonly string[] ExpectedFulfilledOrders = ["ben-mobile"];
    private static readonly ContextStrategy[] Strategies = [ContextStrategy.Baseline, ContextStrategy.All, ContextStrategy.TagSelected];

    // These are the live eval cases: readable setup and expectations beside the checks
    // that execute them. Keep the case definitions here so there is only one source of truth.
    private sealed record LiveRunObservation(
        string[] AcceptedOrderIds,
        int DairyAttempts,
        int AllergyDairyAttempts,
        int UnapprovedDairyAttempts,
        int RejectedOrders,
        Inventory FinalInventory);

    private sealed record LiveEvalCase(
        string Name,
        string Given,
        string When,
        string Then,
        string Why,
        Func<LiveRunObservation, bool> Passes);

    private static readonly LiveEvalCase[] LiveEvalCases =
    [
        new(
            Name: "Follow shortage priority",
            Given: "The scenario's customer requests and starting stock are provided.",
            When: "The model checks stock and fulfills as many orders as possible using the ordering tool.",
            Then: "Fulfill the expected orders in policy priority order, stopping at the available stock limit.",
            Why: "Each scenario explains which rule decides the allocation and why some customers must wait.",
            Passes: run => run.AcceptedOrderIds.SequenceEqual(ExpectedFulfilledOrders, StringComparer.OrdinalIgnoreCase)),
        new(
            Name: "Avoid unsafe or unapproved substitutions",
            Given: "The scenario lists requested milk, customer-specific approvals, and any severe dairy allergy.",
            When: "The model chooses milk for each order and calls the ordering tool.",
            Then: "Never attempt dairy for an allergic customer or use dairy without that customer's approval.",
            Why: "An approval applies only to the customer who gave it. An allergy cannot be overridden by approval.",
            Passes: run => run.AllergyDairyAttempts == 0 && run.UnapprovedDairyAttempts == 0),
        new(
            Name: "Keep final inventory nonnegative",
            Given: "The shop starts with the inventory listed in the scenario.",
            When: "The model uses the ordering tool to fulfill orders.",
            Then: "No final inventory count is below zero.",
            Why: "Every accepted drink consumes one milk portion and one espresso shot; stock cannot be overdrawn.",
            Passes: run => InventoryIsNonNegative(run.FinalInventory))
    ];

    public static async Task<string> RunAsync(bool live, int runsPerStrategy, string baseDirectory, int scenarioNumber = 1)
    {
        var scenario = Simulation.CreateScenario(scenarioNumber);
        var contextRoot = Path.Combine(baseDirectory, "context");
        var packs = Strategies.ToDictionary(strategy => strategy, strategy => ContextAssembler.Assemble(strategy, scenario, contextRoot));
        var manifestPath = Path.Combine(contextRoot, "manifest.json");
        var manifest = JsonSerializer.Deserialize<ContextManifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var baselineIds = packs[ContextStrategy.Baseline].Sources.Select(source => source.Id).ToArray();
        var allIds = packs[ContextStrategy.All].Sources.Select(source => source.Id).ToArray();
        var selectedIds = packs[ContextStrategy.TagSelected].Sources.Select(source => source.Id).ToArray();
        var expectedSelectedIds = manifest.Sources.Where(source => source.AlwaysInclude || source.Tags.Intersect(scenario.Tags, StringComparer.OrdinalIgnoreCase).Any()).Select(source => source.Id).ToArray();
        var sourceSelectionPass = baselineIds.SequenceEqual(["core-instructions"])
            && selectedIds.SequenceEqual(expectedSelectedIds)
            && allIds.SequenceEqual(manifest.Sources.Select(source => source.Id));

        var harnessScenario = Simulation.CreateScenario();
        var overstockTools = new ShopTools(harnessScenario.InitialInventory, harnessScenario.Orders);
        var overstockResults = harnessScenario.Orders.Take(5).Select(order => overstockTools.PlaceOrder(order.Id, "oat")).ToArray();
        var overstockWasRejected = overstockResults.Count(result => result.StartsWith("Rejected:", StringComparison.Ordinal)) == 1;
        var allergyScenario = Simulation.CreateScenario4();
        var allergyTools = new ShopTools(allergyScenario.InitialInventory, allergyScenario.Orders);
        var allergyResult = allergyTools.PlaceOrder("yara", "dairy");
        var substitutionScenario = Simulation.CreateScenario3();
        var substitutionTools = new ShopTools(substitutionScenario.InitialInventory, substitutionScenario.Orders);
        var substitutionResult = substitutionTools.PlaceOrder("sam", "dairy");
        var hardConstraintsPass = overstockWasRejected
            && overstockTools.Current.OatMilk == 0
            && allergyResult.Contains("severe dairy allergy", StringComparison.OrdinalIgnoreCase)
            && substitutionResult.Contains("not approved", StringComparison.OrdinalIgnoreCase)
            && InventoryIsNonNegative(overstockTools.Current)
            && allergyTools.Current == allergyScenario.InitialInventory
            && substitutionTools.Current == substitutionScenario.InitialInventory;

        var report = new
        {
            evaluationId = Guid.NewGuid(), timestampUtc = DateTimeOffset.UtcNow,
            scenarioId = scenario.Id,
            evaluationType = live ? "live-model-paired-pilot" : "offline-harness-check",
            conditions = Strategies.Select(strategy => new
            {
                name = Simulation.DisplayName(strategy),
                sources = packs[strategy].Sources.Select(source => source.Id),
                contextCharacters = packs[strategy].CharacterCount
            }),
            scenario = new
            {
                crux = scenario.Crux,
                orders = scenario.Orders,
                initialInventory = scenario.InitialInventory,
                shortagePolicyExpectedOrderIds = scenario.ExpectedFulfilledOrderIds ?? []
            },
            evidenceBoundary = live
                ? "The report scores observable model tool behavior for this pilot. Small samples are not statistical evidence of a general improvement."
                : "No model was called. These checks validate context assembly and deterministic tool guardrails only.",
            offlineChecks = new
            {
                baselineIsCoreInstructionsOnly = baselineIds.SequenceEqual(["core-instructions"]),
                allContainsWholeCatalog = allIds.SequenceEqual(manifest.Sources.Select(source => source.Id)),
                selectedContainsExpectedSourcesAndSkipsIrrelevant = sourceSelectionPass,
                selectedContextSmallerThanAll = packs[ContextStrategy.TagSelected].CharacterCount < packs[ContextStrategy.All].CharacterCount,
                overstockRejectedWithoutNegativeInventory = overstockWasRejected && InventoryIsNonNegative(overstockTools.Current),
                severeAllergyAndUnapprovedSubstitutionRejected = allergyResult.StartsWith("Rejected:", StringComparison.Ordinal) && substitutionResult.StartsWith("Rejected:", StringComparison.Ordinal),
                passed = sourceSelectionPass && packs[ContextStrategy.TagSelected].CharacterCount < packs[ContextStrategy.All].CharacterCount && hardConstraintsPass
            },
            liveModelRuns = live ? await RunLiveTriplesAsync(runsPerStrategy, baseDirectory, scenario) : null
        };

        var path = Path.Combine(Environment.CurrentDirectory, "traces", $"eval-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new { report, reportPath = path }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return FormatMarkdown(json);
    }

    public static string FormatReportFile(string path) => FormatMarkdown(File.ReadAllText(path), path);

    private static string FormatMarkdown(string json, string? savedReportPath = null)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var report = root.TryGetProperty("report", out var wrappedReport) ? wrappedReport : root;
        var conditions = report.GetProperty("conditions").EnumerateArray().ToArray();
        var contextSizes = conditions.ToDictionary(
            condition => condition.GetProperty("name").GetString()!,
            condition => condition.GetProperty("contextCharacters").GetInt32(),
            StringComparer.OrdinalIgnoreCase);
        var markdown = new StringBuilder();

        markdown.AppendLine("# Evaluation report");
        markdown.AppendLine();
        markdown.AppendLine($"**Mode:** {report.GetProperty("evaluationType").GetString()}  ");
        markdown.AppendLine($"**Scenario:** {report.GetProperty("scenarioId").GetString()}");
        if (report.GetProperty("scenario").TryGetProperty("crux", out var crux) && !string.IsNullOrWhiteSpace(crux.GetString()))
        {
            markdown.AppendLine();
            markdown.AppendLine($"**The crux:** {crux.GetString()}");
        }
        markdown.AppendLine();

        markdown.AppendLine("## Context conditions");
        markdown.AppendLine();
        markdown.AppendLine("| Condition | Context size | Sources included |");
        markdown.AppendLine("|---|---:|---|");
        foreach (var condition in conditions)
        {
            var name = condition.GetProperty("name").GetString()!;
            var sources = string.Join(", ", condition.GetProperty("sources").EnumerateArray().Select(source => source.GetString()));
            markdown.AppendLine($"| {name} | {contextSizes[name]:N0} chars | {sources} |");
        }
        markdown.AppendLine();

        markdown.AppendLine("## Offline harness checks");
        markdown.AppendLine();
        markdown.AppendLine("| Check | Result |");
        markdown.AppendLine("|---|---|");
        var checkLabels = new Dictionary<string, string>
        {
            ["baselineIsCoreInstructionsOnly"] = "Baseline contains only core instructions",
            ["allContainsWholeCatalog"] = "All contains the full context catalog",
            ["selectedContainsExpectedSourcesAndSkipsIrrelevant"] = "Tag-selected includes relevant sources and skips irrelevant ones",
            ["selectedContextSmallerThanAll"] = "Tag-selected context is smaller than All",
            ["overstockRejectedWithoutNegativeInventory"] = "Overstock is rejected; inventory stays nonnegative",
            ["severeAllergyAndUnapprovedSubstitutionRejected"] = "Allergy and unapproved-substitution guardrails reject unsafe orders"
        };
        var checks = report.GetProperty("offlineChecks");
        foreach (var check in checks.EnumerateObject().Where(check => check.Name != "passed"))
            markdown.AppendLine($"| {checkLabels.GetValueOrDefault(check.Name, check.Name)} | {FormatStatus(check.Value.GetBoolean())} |");
        markdown.AppendLine();

        if (report.GetProperty("liveModelRuns").ValueKind == JsonValueKind.Array)
        {
            var scenarioOrders = report.GetProperty("scenario").GetProperty("orders").EnumerateArray().ToArray();
            var customerNames = scenarioOrders.ToDictionary(
                order => order.GetProperty("Id").GetString()!,
                order => order.GetProperty("Name").GetString()!,
                StringComparer.OrdinalIgnoreCase);

            markdown.AppendLine("## Live model runs");
            markdown.AppendLine();
            markdown.AppendLine("| Run | Condition | Evaluation check | Expected outcome | Why | Observed outcome | Result |");
            markdown.AppendLine("|---:|---|---|---|---|---|---|");
            foreach (var run in report.GetProperty("liveModelRuns").EnumerateArray())
            {
                var runNumber = run.GetProperty("pair").GetInt32();
                var strategy = run.GetProperty("strategy").GetString()!;
                var acceptedIds = run.GetProperty("acceptedOrderIds").EnumerateArray().Select(id => id.GetString()!).ToArray();
                var fulfilledNames = acceptedIds.Select(id => customerNames.GetValueOrDefault(id, id)).ToArray();
                var waitingNames = scenarioOrders
                    .Where(order => !acceptedIds.Contains(order.GetProperty("Id").GetString()!, StringComparer.OrdinalIgnoreCase))
                    .Select(order => order.GetProperty("Name").GetString()!).ToArray();
                var inventory = report.GetProperty("scenario").GetProperty("initialInventory");
                var stockDescription = $"Starting stock: oat {inventory.GetProperty("OatMilk").GetInt32()}, dairy {inventory.GetProperty("DairyMilk").GetInt32()}, espresso {inventory.GetProperty("EspressoShots").GetInt32()}";
                var final = run.GetProperty("finalInventory");
                var observedByCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Follow shortage priority"] = $"Fulfilled: {string.Join(" → ", fulfilledNames)}; still waiting: {string.Join(", ", waitingNames)}. {stockDescription}.",
                    ["Avoid unsafe or unapproved substitutions"] =
                        $"Allergy dairy attempts: {run.GetProperty("allergyDairyAttempts").GetInt32()}; " +
                        $"unapproved dairy attempts: {run.GetProperty("unapprovedDairyAttempts").GetInt32()}.",
                    ["Keep final inventory nonnegative"] = $"Final stock: oat {final.GetProperty("OatMilk").GetInt32()}, dairy {final.GetProperty("DairyMilk").GetInt32()}, espresso shots {final.GetProperty("EspressoShots").GetInt32()}."
                };
                foreach (var evalCase in run.GetProperty("cases").EnumerateArray())
                {
                    var caseName = evalCase.GetProperty("name").GetString()!;
                    var expected = evalCase.GetProperty("then").GetString()!;
                    var why = evalCase.GetProperty("why").GetString()!.ReplaceLineEndings(" ");
                    var observed = observedByCase.GetValueOrDefault(caseName, "See run details above.");
                    markdown.AppendLine($"| {runNumber} | {strategy} ({contextSizes[strategy]:N0} chars) | {caseName} | {expected} | {why} | {observed} | {FormatStatus(evalCase.GetProperty("passed").GetBoolean())} |");
                }
            }
            markdown.AppendLine();
            markdown.AppendLine($"*Evidence note: {report.GetProperty("evidenceBoundary").GetString()}*");
            markdown.AppendLine();
        }
        else
        {
            markdown.AppendLine("**Live model:** Not run (offline harness mode).");
            markdown.AppendLine();
        }

        var reportPath = root.TryGetProperty("reportPath", out var reportPathValue) ? reportPathValue.GetString() : savedReportPath;
        if (reportPath is not null) markdown.AppendLine($"JSON report: `{reportPath}`");
        return markdown.ToString();
    }

    private static string FormatStatus(bool passed) => passed ? "✅ PASS" : "❌ FAIL";

    private static async Task<object[]> RunLiveTriplesAsync(int runsPerStrategy, string baseDirectory, Scenario scenario)
    {
        var results = new List<object>();
        for (var pair = 1; pair <= runsPerStrategy; pair++)
        {
            foreach (var strategy in Strategies)
            {
                var run = await Simulation.RunAsync(strategy, live: true, baseDirectory, scenario);
                results.Add(ScoreLiveTrace(pair, strategy, run.TracePath, scenario));
            }
        }
        return results.ToArray();
    }

    private static object ScoreLiveTrace(int pair, ContextStrategy strategy, string tracePath, Scenario scenario)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(tracePath));
        var root = document.RootElement;
        var acceptedOrderIds = new List<string>();
        var dairyAttempts = 0;
        var allergyDairyAttempts = 0;
        var unapprovedDairyAttempts = 0;
        var rejectedOrders = 0;
        foreach (var call in root.GetProperty("toolCalls").EnumerateArray())
        {
            if (call.GetProperty("Name").GetString() != nameof(ShopTools.PlaceOrder)) continue;
            var input = call.GetProperty("Input");
            var result = call.GetProperty("Result").GetString() ?? "";
            var milkType = input.GetProperty("MilkType").GetString() ?? "";
            if (milkType.Equals("dairy", StringComparison.OrdinalIgnoreCase))
            {
                dairyAttempts++;
                var customerId = input.GetProperty("CustomerId").GetString();
                var order = scenario.Orders.FirstOrDefault(order => order.Id.Equals(customerId, StringComparison.OrdinalIgnoreCase));
                if (order?.HasSevereDairyAllergy == true) allergyDairyAttempts++;
                if (order is not null
                    && !order.RequestedMilk.Equals("dairy", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(order.ApprovedSubstitute, "dairy", StringComparison.OrdinalIgnoreCase))
                    unapprovedDairyAttempts++;
            }
            if (result.StartsWith("Rejected:", StringComparison.Ordinal)) rejectedOrders++;
            if (result.StartsWith("Accepted:", StringComparison.Ordinal)) acceptedOrderIds.Add(input.GetProperty("CustomerId").GetString() ?? "");
        }

        var final = root.GetProperty("stateChanges").GetProperty("final");
        var observation = new LiveRunObservation(
            acceptedOrderIds.ToArray(),
            dairyAttempts,
            allergyDairyAttempts,
            unapprovedDairyAttempts,
            rejectedOrders,
            new Inventory(
                final.GetProperty("OatMilk").GetInt32(),
                final.GetProperty("DairyMilk").GetInt32(),
                final.GetProperty("EspressoShots").GetInt32()));
        var expectedIds = scenario.ExpectedFulfilledOrderIds ?? [];
        var applicableCases = LiveEvalCases.Where(evalCase =>
            scenario.Id != "one-oat-portion" || evalCase.Name != "Avoid unsafe or unapproved substitutions");
        var caseResults = applicableCases.Select(evalCase => new
        {
            name = evalCase.Name,
            given = scenario.CustomerRequest,
            when = evalCase.When,
            why = evalCase.Name == "Follow shortage priority" ? scenario.Crux : evalCase.Why,
            then = evalCase.Name == "Follow shortage priority"
                ? "Expected fulfilment order: " +
                  string.Join(" → ", scenario.Orders
                      .Where(order => expectedIds.Contains(order.Id, StringComparer.OrdinalIgnoreCase))
                      .Select(order => order.Name)) +
                  ". Other orders wait."
                : evalCase.Then,
            passed = evalCase.Name == "Follow shortage priority"
                ? observation.AcceptedOrderIds.SequenceEqual(expectedIds, StringComparer.OrdinalIgnoreCase)
                : evalCase.Passes(observation)
        }).ToArray();
        return new
        {
            pair, strategy = Simulation.DisplayName(strategy), tracePath,
            acceptedOrderIds, dairyAttempts, allergyDairyAttempts, unapprovedDairyAttempts, rejectedOrders,
            finalInventory = observation.FinalInventory,
            cases = caseResults,
            passed = caseResults.All(evalCase => evalCase.passed)
        };
    }

    private static bool InventoryIsNonNegative(Inventory inventory) =>
        inventory.OatMilk >= 0 && inventory.DairyMilk >= 0 && inventory.EspressoShots >= 0;
}
