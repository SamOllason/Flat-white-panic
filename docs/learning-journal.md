# Learning journal

### Experiment

Compare three fixed context conditions for the Great Oat Milk Shortage: core instructions only (Baseline), the full context catalog (All), and a deterministic manifest/tag-selected set. The shortage policy prioritizes prepaid mobile orders, then walk-ins by arrival.

Scope distinction: the application assembles each pack before execution and supplies it as agent instructions. The agent does not retrieve context dynamically. This experiment studies the effect of different supplied context, not a context mill or retrieval system.

### Hypothesis

Baseline may not allocate scarce stock according to the shop's prepaid-first policy. All and Tag-selected should follow that policy. Tag-selected should use less context than All while preserving the expected outcomes. Offline harness checks cannot verify model behavior; repeated live model runs are needed for that.

### Change

Initial setup: all three conditions share the same request, tools, scenario, inventory, and model configuration. The context assembler reads a versioned manifest and applies an exact scenario-tag match to select Markdown sources before the run. A deterministic offline eval checks source selection, context size, inventory bounds, allergy rejection, and substitution approval.

### Observations

Not yet recorded.

### Evidence

Offline eval results validate context assembly and deterministic tool rules only, not model behavior or the prepaid-first decision. Live model behavior is recorded separately in the per-scenario trace reports; these small pilots are observations, not evidence of general improvement.

### Lessons

Not yet recorded.

### Next questions

How should we control run order, sampling variability, and repeated trials when comparing the three context conditions?
# Scenario expansion: policy exceptions and interacting constraints

Added three small, named scenarios to create clearer opportunities for context to affect observable tool behavior. Scenario 2 tests a pickup-window exception to mobile-order priority. Scenario 3 tests customer-specific substitution approvals alongside an allergy. Scenario 4 tests safe priority under a shared espresso bottleneck. Each scenario's short “crux” is shown in console output and the eval report. These are hypotheses for future live runs, not evidence of model improvement. Each live scenario uses three calls, one for each context condition.
