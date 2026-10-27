using FlatWhitePanic;

DotEnv.Load(Path.Combine(Environment.CurrentDirectory, ".env"));

var scenarioNumber = args.Contains("scenario3", StringComparer.OrdinalIgnoreCase) ? 3 : args.Contains("scenario2", StringComparer.OrdinalIgnoreCase) ? 2 : 1;
var scenarioIndex = Array.FindIndex(args, arg => arg.Equals("--scenario", StringComparison.OrdinalIgnoreCase));
if (scenarioIndex >= 0 && scenarioIndex + 1 < args.Length && int.TryParse(args[scenarioIndex + 1], out var selectedScenarioNumber))
    scenarioNumber = selectedScenarioNumber;

if (args.FirstOrDefault()?.Equals("eval", StringComparison.OrdinalIgnoreCase) == true)
{
    var reportIndex = Array.FindIndex(args, arg => arg.Equals("--format-report", StringComparison.OrdinalIgnoreCase));
    if (reportIndex >= 0)
    {
        if (reportIndex + 1 >= args.Length)
            throw new ArgumentException("Provide a JSON report path after --format-report.");
        Console.WriteLine(Evaluation.FormatReportFile(args[reportIndex + 1]));
        return;
    }

    var evalLive = args.Contains("--live", StringComparer.OrdinalIgnoreCase);
    var runsIndex = Array.FindIndex(args, arg => arg.Equals("--runs", StringComparison.OrdinalIgnoreCase));
    var runs = runsIndex >= 0 && runsIndex + 1 < args.Length && int.TryParse(args[runsIndex + 1], out var parsedRuns)
        ? Math.Clamp(parsedRuns, 1, 20)
        : 1;
    Console.WriteLine(await Evaluation.RunAsync(evalLive, runs, AppContext.BaseDirectory, scenarioNumber));
    return;
}

var strategy = args.FirstOrDefault()?.ToLowerInvariant() switch
{
    "baseline" => ContextStrategy.Baseline,
    "all" => ContextStrategy.All,
    "curated" => ContextStrategy.Curated,
    _ => ContextStrategy.Curated
};

var live = args.Contains("--live", StringComparer.OrdinalIgnoreCase);
var root = AppContext.BaseDirectory;
var selectedScenario = Simulation.CreateScenario(scenarioNumber);
Console.WriteLine($"THE CRUX: {selectedScenario.Crux}\n");
var run = await Simulation.RunAsync(strategy, live, root, selectedScenario);
Console.WriteLine(run.ConsoleTrace);
Console.WriteLine($"\nTrace written to: {run.TracePath}");
