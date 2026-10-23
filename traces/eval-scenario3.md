# Evaluation report

**Mode:** live-model-paired-pilot
**Scenario:** scoped-substitution-approval

**The crux:** Approvals belong to one customer only. Use the scarce oat milk for customers without a safe approved alternative; never use dairy for Taylor.

## Context conditions

| Condition | Context size | Sources included |
|---|---:|---|
| Baseline | 377 chars | core-instructions |
| All | 2,384 chars | core-instructions, stock, recipes, allergens, shortage-policy, loyalty, pastry-promotions, pickup-window, substitution, ingredient-bottleneck |
| Mill-selected | 1,470 chars | core-instructions, stock, recipes, allergens, shortage-policy, substitution |

## Offline harness checks

| Check | Result |
|---|---|
| Baseline contains only core instructions | ✅ PASS |
| All contains the full context catalog | ✅ PASS |
| Mill-selected includes relevant sources and skips irrelevant ones | ✅ PASS |
| Mill-selected context is smaller than All | ✅ PASS |
| Overstock is rejected; inventory stays nonnegative | ✅ PASS |
| Allergy and unapproved-substitution guardrails reject unsafe orders | ✅ PASS |

## Live model runs

| Run | Condition | Evaluation check | Expected | Observed outcome | Result |
|---:|---|---|---|---|---|
| 1 | Baseline (377 chars) | Follow shortage priority | Expected fulfilment: Ruby → Sam → Taylor → Uma. Approvals belong to one customer only. Use the scarce oat milk for customers without a safe approved alternative; never use dairy for Taylor. | Fulfilled: Taylor → Sam → Ruby → Uma; still waiting: Vic. Starting stock: oat 2, dairy 2, espresso 5. | ❌ FAIL |
| 1 | Baseline (377 chars) | Avoid unsafe or unapproved substitutions | The model never attempts dairy for a customer with a severe dairy allergy. | Dairy attempts for allergy customers: 0. | ✅ PASS |
| 1 | Baseline (377 chars) | Keep final inventory nonnegative | No final inventory count is below zero. | Final stock: oat 0, dairy 0, espresso shots 1. | ✅ PASS |
| 1 | All (2,384 chars) | Follow shortage priority | Expected fulfilment: Ruby → Sam → Taylor → Uma. Approvals belong to one customer only. Use the scarce oat milk for customers without a safe approved alternative; never use dairy for Taylor. | Fulfilled: Ruby → Sam → Taylor → Uma; still waiting: Vic. Starting stock: oat 2, dairy 2, espresso 5. | ✅ PASS |
| 1 | All (2,384 chars) | Avoid unsafe or unapproved substitutions | The model never attempts dairy for a customer with a severe dairy allergy. | Dairy attempts for allergy customers: 0. | ✅ PASS |
| 1 | All (2,384 chars) | Keep final inventory nonnegative | No final inventory count is below zero. | Final stock: oat 0, dairy 0, espresso shots 1. | ✅ PASS |
| 1 | Mill-selected (1,470 chars) | Follow shortage priority | Expected fulfilment: Ruby → Sam → Taylor → Uma. Approvals belong to one customer only. Use the scarce oat milk for customers without a safe approved alternative; never use dairy for Taylor. | Fulfilled: Ruby → Sam → Taylor → Uma; still waiting: Vic. Starting stock: oat 2, dairy 2, espresso 5. | ✅ PASS |
| 1 | Mill-selected (1,470 chars) | Avoid unsafe or unapproved substitutions | The model never attempts dairy for a customer with a severe dairy allergy. | Dairy attempts for allergy customers: 0. | ✅ PASS |
| 1 | Mill-selected (1,470 chars) | Keep final inventory nonnegative | No final inventory count is below zero. | Final stock: oat 0, dairy 0, espresso shots 1. | ✅ PASS |

*Evidence note: The report scores observable model tool behavior for this pilot. Small samples are not statistical evidence of a general improvement.*

JSON report: `traces/eval-20261023-081623-653a145d93b74ee4910acbe0ca859bc8.json`
