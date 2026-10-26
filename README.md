# Flat White Panic ☕

A small .NET 10 console experiment about context engineering. The main scenario is deliberately tiny: Ava arrives first as a walk-in, Ben places a prepaid order a few minutes later, and only one oat flat white can be made.

The app uses evals to compare three context conditions using the same scenario, tools, and initial stock:

- **Baseline** sends only essential instructions. Scenario facts and inventory are still supplied in the request and tool results, but the shop's prepaid-first shortage policy is not.
- **All** sends every source in the context manifest, including unrelated loyalty and pastry notes.
- **Tag-selected** always includes essential instructions, then deterministically selects sources whose manifest tags match the scenario. It supplies the prepaid-first shortage policy.

This slice compares fixed context conditions: application code assembles each context pack before the agent run and passes it as instructions. The agent does not search, retrieve, or choose context dynamically. The current tag matcher is a simple deterministic selector; “context mill” is a broader architectural idea and not a capability this experiment implements.

Run the deterministic scripted demo without credentials:

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

The scripted demo follows request order (FIFO) to exercise the tools; it does not follow the shop policy and is not evidence of model behavior. Use the eval command below to compare live model decisions.

Run the offline harness eval for the simple main scenario:

```powershell
dotnet run --project FlatWhitePanic -- eval
```

The more complex scenarios remain available as exploratory follow-ups:

```powershell
dotnet run --project FlatWhitePanic -- eval --scenario 2
dotnet run --project FlatWhitePanic -- eval --scenario 3
dotnet run --project FlatWhitePanic -- eval --scenario 4
```

Evals print a readable Markdown summary in the console and save detailed JSON under `traces/`. Reprint a saved report without rerunning the model:

```powershell
dotnet run --project FlatWhitePanic -- eval --format-report traces/<report-file>.json
```

Scenarios 2–4 are optional exploratory cases:

- **Scenario 2 — Pickup-window exception:** five orders, two oat portions. A walk-in pickup due within 10 minutes outranks a later prepaid order, testing whether the model notices a specific exception to the usual prepaid-first rule.
- **Scenario 3 — Scoped substitution approval:** five orders, two oat and two dairy portions. Two customers approve dairy substitutions, one has a severe dairy allergy, and approvals apply only to the named customer.
- **Scenario 4 — Three-ingredient bottleneck:** five orders, three espresso shots, two oat portions, and one dairy portion. Milk and espresso are tied bottlenecks; the model must obey priority and reject an unsafe dairy request.

The live eval makes three model calls (one per context condition). Use `--runs 3` to repeat each condition three times (nine calls total).

It checks that Baseline contains only core instructions, Tag-selected includes the files matched by the scenario tags, context size is reduced, and deterministic tools reject overstock, allergens, and unapproved substitutions. It does not call a model or evaluate model behavior. Evals print a readable Markdown summary in the console and save the detailed JSON report under `traces/`; each demo also writes a JSON trace there.

For an opt-in model pilot, put the Foundry project endpoint and deployment in `.env`, then sign in once with `azd auth login`. The app reads `.env` automatically; existing shell environment variables take precedence. Do not put credentials or tokens in `.env`. The default live eval runs the simple two-customer scenario:

```powershell
dotnet run --project FlatWhitePanic -- eval --live
```

Use `--scenario 2`, `--scenario 3`, or `--scenario 4` to try one of the exploratory scenarios instead.

Use `--runs 3` only when you want three repeats of the selected scenario under each context condition (nine calls total).

The main live pilot checks whether Ben gets the last oat portion and whether inventory stays nonnegative. Baseline versus Tag-selected compares behavior with and without the prepaid-first rule; All versus Tag-selected compares the full catalog with the relevant rule. Allergy and substitution guardrails are exercised by the exploratory scenarios. A small sample is not statistical proof of improvement. The model package follows the current Microsoft Agent Framework Foundry provider documented in [Microsoft Learn](https://learn.microsoft.com/en-us/agent-framework/agents/providers/microsoft-foundry). Run focused tests with `dotnet test`.
