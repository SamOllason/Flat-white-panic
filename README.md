# Flat White Panic ☕

A small .NET 10 console experiment about context engineering. A coffee shop manager handles six oat flat white requests when only four portions of oat milk remain and one customer has a severe dairy allergy.

The app uses evals to compare three context conditions using the same scenario, tools, and initial stock:

- **Baseline** sends only essential instructions. Scenario facts and inventory are still supplied in the request and tool results.
- **All** sends every source in the context manifest, including unrelated loyalty and pastry notes.
- **Mill-selected** always includes essential instructions, then selects relevant sources by scenario tags. Its shortage policy prioritizes prepaid mobile orders, then walk-ins by arrival order.

Run either deterministic scripted demo without credentials:

```powershell
dotnet run --project FlatWhitePanic -- baseline
dotnet run --project FlatWhitePanic -- all
dotnet run --project FlatWhitePanic -- selected
```

Run the offline harness eval:

```powershell
dotnet run --project FlatWhitePanic -- eval
```

Evals print a readable Markdown summary in the console and save detailed JSON under `traces/`. Reprint a saved report without rerunning the model:

```powershell
dotnet run --project FlatWhitePanic -- eval --format-report traces/<report-file>.json
```

The live behavior cases are written as named Given/When/Then checks in `FlatWhitePanic/Evaluation.cs`; the eval summary includes each case and its pass result. Scenario 1 starts with one customer and one order. Scenario 2 adds the shortage policy and allergy constraints. The live eval makes six model calls total: two scenarios across three context conditions.

It checks that Baseline contains only core instructions, the mill includes the expected relevant files and excludes unrelated files, context size is reduced, and deterministic tools reject overstock, allergens, and unapproved substitutions. It does not call a model or evaluate model behavior. Evals print a readable Markdown summary in the console and save the detailed JSON report under `traces/`; each demo also writes a JSON trace there.

For an opt-in model pilot, put the Foundry project endpoint and deployment in `.env`, then sign in once with `azd auth login`. The app reads `.env` automatically; existing shell environment variables take precedence. Do not put credentials or tokens in `.env`. The eval makes one model call per scenario and condition (six calls total):

```powershell
dotnet run --project FlatWhitePanic -- eval --live
```

Use `--runs 3` only when you want three repeats of every scenario and context condition (eighteen calls total).

The live pilot grades observable tool behavior: fulfill the two prepaid orders first, then the first two walk-ins, attempt no dairy order, and leave inventory nonnegative. Baseline versus Mill-selected tests the effect of supplying the policy; All versus Mill-selected tests whether the mill preserves that behavior while omitting unrelated context. A small sample is not statistical proof of improvement. The model package follows the current Microsoft Agent Framework Foundry provider documented in [Microsoft Learn](https://learn.microsoft.com/en-us/agent-framework/agents/providers/microsoft-foundry). Run focused tests with `dotnet test`.
