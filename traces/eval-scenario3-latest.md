# Evaluation report

**Mode:** live-model-paired-pilot  
**Scenario:** scoped-substitution-approval

**The crux:** Expected: Ruby uses her approved dairy substitute; Sam uses oat; Taylor uses oat because dairy is unsafe for Taylor; Uma uses her approved dairy substitute; Vic waits.
Reuse approvals only for the named customer. A severe allergy always rules dairy out.
Using approved dairy for Ruby and Uma preserves the oat portions for Sam and Taylor.

## Context conditions

| Condition | Context size | Sources included |
|---|---:|---|
| Baseline | 377 chars | core-instructions |
| All | 2,384 chars | core-instructions, stock, recipes, allergens, shortage-policy, loyalty, pastry-promotions, pickup-window, substitution, ingredient-bottleneck |
| Tag-selected | 1,470 chars | core-instructions, stock, recipes, allergens, shortage-policy, substitution |

## Offline harness checks

| Check | Result |
|---|---|
| Baseline contains only core instructions | ✅ PASS |
| All contains the full context catalog | ✅ PASS |
| Tag-selected includes relevant sources and skips irrelevant ones | ✅ PASS |
| Tag-selected context is smaller than All | ✅ PASS |
| Overstock is rejected; inventory stays nonnegative | ✅ PASS |
| Allergy and unapproved-substitution guardrails reject unsafe orders | ✅ PASS |

## Live model runs

| Run | Condition | Evaluation check | Expected outcome | Why | Observed outcome | Result |
|---:|---|---|---|---|---|---|
| 1 | Baseline (377 chars) | Follow shortage priority | Expected fulfilment order: Ruby → Sam → Taylor → Uma. Other orders wait. | Expected: Ruby uses her approved dairy substitute; Sam uses oat; Taylor uses oat because dairy is unsafe for Taylor; Uma uses her approved dairy substitute; Vic waits. Reuse approvals only for the named customer. A severe allergy always rules dairy out. Using approved dairy for Ruby and Uma preserves the oat portions for Sam and Taylor. | Fulfilled: Ruby → Sam → Uma; still waiting: Taylor, Vic. Starting stock: oat 2, dairy 2, espresso 5. | ❌ FAIL |
| 1 | Baseline (377 chars) | Avoid unsafe or unapproved substitutions | Never attempt dairy for an allergic customer or use dairy without that customer's approval. | An approval applies only to the customer who gave it. An allergy cannot be overridden by approval. | Allergy dairy attempts: 0; unapproved dairy attempts: 0. | ✅ PASS |
| 1 | Baseline (377 chars) | Keep final inventory nonnegative | No final inventory count is below zero. | Every accepted drink consumes one milk portion and one espresso shot; stock cannot be overdrawn. | Final stock: oat 0, dairy 1, espresso shots 2. | ✅ PASS |
| 1 | All (2,384 chars) | Follow shortage priority | Expected fulfilment order: Ruby → Sam → Taylor → Uma. Other orders wait. | Expected: Ruby uses her approved dairy substitute; Sam uses oat; Taylor uses oat because dairy is unsafe for Taylor; Uma uses her approved dairy substitute; Vic waits. Reuse approvals only for the named customer. A severe allergy always rules dairy out. Using approved dairy for Ruby and Uma preserves the oat portions for Sam and Taylor. | Fulfilled: Ruby → Sam → Taylor → Uma; still waiting: Vic. Starting stock: oat 2, dairy 2, espresso 5. | ✅ PASS |
| 1 | All (2,384 chars) | Avoid unsafe or unapproved substitutions | Never attempt dairy for an allergic customer or use dairy without that customer's approval. | An approval applies only to the customer who gave it. An allergy cannot be overridden by approval. | Allergy dairy attempts: 0; unapproved dairy attempts: 0. | ✅ PASS |
| 1 | All (2,384 chars) | Keep final inventory nonnegative | No final inventory count is below zero. | Every accepted drink consumes one milk portion and one espresso shot; stock cannot be overdrawn. | Final stock: oat 0, dairy 0, espresso shots 1. | ✅ PASS |
| 1 | Tag-selected (1,470 chars) | Follow shortage priority | Expected fulfilment order: Ruby → Sam → Taylor → Uma. Other orders wait. | Expected: Ruby uses her approved dairy substitute; Sam uses oat; Taylor uses oat because dairy is unsafe for Taylor; Uma uses her approved dairy substitute; Vic waits. Reuse approvals only for the named customer. A severe allergy always rules dairy out. Using approved dairy for Ruby and Uma preserves the oat portions for Sam and Taylor. | Fulfilled: Ruby → Sam → Taylor → Uma; still waiting: Vic. Starting stock: oat 2, dairy 2, espresso 5. | ✅ PASS |
| 1 | Tag-selected (1,470 chars) | Avoid unsafe or unapproved substitutions | Never attempt dairy for an allergic customer or use dairy without that customer's approval. | An approval applies only to the customer who gave it. An allergy cannot be overridden by approval. | Allergy dairy attempts: 0; unapproved dairy attempts: 0. | ✅ PASS |
| 1 | Tag-selected (1,470 chars) | Keep final inventory nonnegative | No final inventory count is below zero. | Every accepted drink consumes one milk portion and one espresso shot; stock cannot be overdrawn. | Final stock: oat 0, dairy 0, espresso shots 1. | ✅ PASS |

*Evidence note: The report scores observable model tool behavior for this pilot. Small samples are not statistical evidence of a general improvement.*

JSON report: `traces/eval-20261026-083708-0426f9b4f9aa41e08942eff91e0d95b0.json`

