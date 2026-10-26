# Learning journal

### Main experiment: one remaining oat flat white

Two customers need oat flat whites. Ava arrived first as a walk-in; Ben placed a prepaid mobile order a few minutes later. Only one oat portion remains. The customer request provides these facts and asks the agent to choose which order has priority, but does not state the shop's policy. The shortage-policy context says prepaid orders come first.

Compare three fixed context conditions: Baseline (core instructions only), All (the full catalog), and Tag-selected (the relevant shortage policy and essential instructions). The app assembles each pack before execution; the agent does not retrieve context dynamically.

### Hypothesis

The prepaid-first policy should make Ben more likely to receive the last oat flat white. Tag-selected should provide that rule with less context than All. If Baseline also chooses Ben, this scenario has not shown a difference; record that result honestly and consider repeated runs.

### Evaluation

The live behavior check records which order the agent actually fulfills using the tool. It checks that Ben is selected under the stated shop policy and that inventory does not go negative. Offline harness checks separately verify context source selection, context size, and deterministic tool guardrails. A live pass or failure is an observation from a model run, not a unit test of implementation correctness or proof of general model quality.

### Earlier exploratory scenarios

The previous versions explored a pickup-window exception, customer-specific dairy approvals plus an allergy, and a multiple-ingredient bottleneck. They remain available as follow-up scenarios, but are more complex than needed to introduce the basic learning point.

### Evidence boundary

The context is tiny compared with a model's context window. This experiment asks whether a relevant business rule changes an observable action—not whether a context mill improves token cost or scales to production. The selector is deterministic and runs before the agent call.

### Next questions

Do repeated runs show a consistent difference between Baseline, All, and Tag-selected? If they do, how many runs are needed to distinguish an effect from model variability?
