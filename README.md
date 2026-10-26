# Flat White Panic ☕

A small .NET 10 console experiment about context engineering. A coffee shop manager handles six oat flat white requests when only four portions of oat milk remain and one customer has a severe dairy allergy.

The app uses evals to compare three context conditions using the same scenario, tools, and initial stock:

- **Baseline** sends only essential instructions. Scenario facts and inventory are still supplied in the request and tool results.
- **All** sends every source in the context manifest, including unrelated loyalty and pastry notes.
- **Tag-selected** always includes essential instructions, then deterministically selects sources whose manifest tags match the scenario. Its shortage policy prioritizes prepaid mobile orders, then walk-ins by arrival order.

This slice compares fixed context conditions: application code assembles each context pack before the agent run and passes it as instructions. The agent does not search, retrieve, or choose context dynamically. The current tag matcher is a simple deterministic selector; “context mill” is a broader architectural idea and not a capability this experiment implements.

Run either deterministic scripted demo without credentials:

```powershell
dotnet run --project FlatWhitePanic -- baseline
dotnet run --project FlatWhitePanic -- all
dotnet run --project FlatWhitePanic -- selected
```

Run a scenario in scripted demo mode (no model call):

```powershell
dotnet run --project FlatWhitePanic -- scenario2
dotnet run --project FlatWhitePanic -- scenario3
dotnet run --project FlatWhitePanic -- scenario4
```

Run the offline harness eval for a chosen scenario (defaults to Scenario 1):

```powershell
dotnet run --project FlatWhitePanic -- eval
dotnet run --project FlatWhitePanic -- eval --scenario 2
dotnet run --project FlatWhitePanic -- eval --scenario 3
dotnet run --project FlatWhitePanic -- eval --scenario 4
```

Evals print a readable Markdown summary in the console and save detailed JSON under `traces/`. Reprint a saved report without rerunning the model:

```powershell
dotnet run --project FlatWhitePanic -- eval --format-report traces/<report-file>.json
```

The live behavior cases are written as named Given/When/Then checks in `FlatWhitePanic/Evaluation.cs`; the eval summary includes each case and its pass result. Each new scenario prints its crux before the run:

- **Scenario 2 — Pickup-window exception:** five orders, two oat portions. A walk-in pickup due within 10 minutes outranks a later prepaid order, testing whether the model notices a specific exception to the usual prepaid-first rule.
- **Scenario 3 — Scoped substitution approval:** five orders, two oat and two dairy portions. Two customers approve dairy substitutions, one has a severe dairy allergy, and approvals apply only to the named customer.
- **Scenario 4 — Three-ingredient bottleneck:** five orders, three espresso shots, two oat portions, and one dairy portion. Milk and espresso are tied bottlenecks; the model must obey priority and reject an unsafe dairy request.

The per-scenario live eval makes three model calls (one per context condition). Run one first to keep the pilot small; use `--runs 3` only when you want repeats.

It checks that Baseline contains only core instructions, Tag-selected includes the files matched by the scenario tags, context size is reduced, and deterministic tools reject overstock, allergens, and unapproved substitutions. It does not call a model or evaluate model behavior. Evals print a readable Markdown summary in the console and save the detailed JSON report under `traces/`; each demo also writes a JSON trace there.

For an opt-in model pilot, put the Foundry project endpoint and deployment in `.env`, then sign in once with `azd auth login`. The app reads `.env` automatically; existing shell environment variables take precedence. Do not put credentials or tokens in `.env`. Choose a scenario for the live eval:

```powershell
dotnet run --project FlatWhitePanic -- eval --scenario 2 --live
```

Use `--runs 3` only when you want three repeats of every scenario and context condition (eighteen calls total).

The live pilot checks expected fulfillment order, attempts to serve dairy to an allergic customer, and nonnegative inventory. Baseline versus Tag-selected tests the effect of supplying the scenario policy; All versus Tag-selected tests whether the tag-selected pack preserves behavior while omitting unrelated context. A small sample is not statistical proof of improvement. The model package follows the current Microsoft Agent Framework Foundry provider documented in [Microsoft Learn](https://learn.microsoft.com/en-us/agent-framework/agents/providers/microsoft-foundry). Run focused tests with `dotnet test`.
