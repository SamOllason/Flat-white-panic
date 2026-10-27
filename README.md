# Flat White Panic ☕

A small .NET 10 console experiment about context engineering. Two customers want one oat flat white each, but the shop has only one oat portion. Ava places her order first, but Ben has the earlier promised pickup time.

## What it compares

The same task runs under three context conditions:

- **Baseline**: essential instructions only. The request includes the customer, timing, and stock facts, but not the shop's pickup-time policy.
- **All**: the full context catalog, including unrelated loyalty and pastry notes.
- **Curated**: essential instructions plus the context relevant to this scenario.

The policy says to fulfill the earliest promised pickup first. In Scenario 1, Ben gets the coffee even though Ava placed her order first. Scenario 2 swaps only the pickup times, so Ava should get it. Scenario 3 gives both orders the same pickup time; its tie-break favors a reserved pickup over the walk-in queue.

“Curated” describes the context the agent receives, not how it was chosen. In this prototype, application code deterministically matches scenario tags in a manifest. A future context mill could choose relevant material dynamically, as PostHog’s does; this app does not do that yet.

## Run the scripted demo

```powershell
dotnet run --project FlatWhitePanic -- baseline
dotnet run --project FlatWhitePanic -- all
dotnet run --project FlatWhitePanic -- curated
```

The scripted demo follows request order (FIFO) and is only a harness demonstration, not evidence about model behavior.

## Run the eval

By default, eval runs Scenario 1. Use `--scenario 2` for the mirrored pickup-time case or `--scenario 3` for the reserved-slot tie-break. All three use the same context conditions.

Offline checks, without a model call:

```powershell
dotnet run --project FlatWhitePanic -- eval
dotnet run --project FlatWhitePanic -- eval --scenario 2
dotnet run --project FlatWhitePanic -- eval --scenario 3
```

Live pilot, one model call per context condition:

```powershell
dotnet run --project FlatWhitePanic -- eval --live
dotnet run --project FlatWhitePanic -- eval --scenario 2 --live
dotnet run --project FlatWhitePanic -- eval --scenario 3 --live
```

Use `--runs 3` to repeat each condition three times (nine calls total). Live mode needs `FOUNDRY_PROJECT_ENDPOINT` and `FOUNDRY_MODEL_DEPLOYMENT` in `.env`, plus `azd auth login`. Do not put credentials or tokens in `.env`.

The offline harness checks context selection, context size, overstock rejection, and nonnegative inventory. The live eval checks which order the model fulfills and the resulting stock. Offline checks are not model evaluations, and a small live sample is not statistical evidence of general improvement. Reports are printed as Markdown and saved as JSON under `traces/`.

The project uses Microsoft's Agent Framework Foundry provider. See [the official provider guide](https://learn.microsoft.com/en-us/agent-framework/agents/providers/microsoft-foundry). Run the test suite with `dotnet test`.
