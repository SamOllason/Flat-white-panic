# Learning journal

### Experiment

Compare three context conditions for the Great Oat Milk Shortage: core instructions only (Baseline), the full context catalog (All), and a tag-based mill-selected set. The shortage policy prioritizes prepaid mobile orders, then walk-ins by arrival.

### Hypothesis

Baseline may not allocate scarce stock according to the shop's prepaid-first policy. All and Mill-selected should follow that policy. Mill-selected should use less context than All while preserving the expected outcomes. Offline harness checks cannot verify model behavior; repeated live model runs are needed for that.

### Change

Initial setup: all three conditions share the same request, tools, scenario, inventory, and model configuration. The context mill reads a versioned manifest and selects Markdown sources by scenario tags. A deterministic offline eval checks source selection, context size, inventory bounds, allergy rejection, and substitution approval.

### Observations

Not yet recorded.

### Evidence

No model comparison has been run. Offline eval results validate context assembly and deterministic tool rules only, not model behavior or the prepaid-first decision.

### Lessons

Not yet recorded.

### Next questions

How should we control run order, sampling variability, and repeated trials when comparing the three context conditions?
