---
layout: post
title: "Context Engineering, Served with Oat Milk!"
date: 2026-10-23
categories: [AI, Azure, Foundry, Context Engineering]
---

# Introduction
I've been reading about 'context engineering' and wanted to understand it better.

TL;DR: In one small, deliberately contrived but very minimal coffee-themed example, adding a shop rule to the context changed the model’s choice. Which was interesting!

(Keep an eye out for a few really bad coffee jokes. I've _blended_ them into the serious analysis 😉.)

# What I did
I brewed up a small coffee-themed project to make context engineering tangible: does giving an agent a relevant business rule change what it does? I took inspiration from [PostHog's context mill](https://posthog.com/newsletter/context-engineering), which _dynamically_ assembles maintained knowledge for its agents at runtime.

And, like with any new software, its fun to try and understand what it doe by trying to find its limit and breaking it!

I worked with Codex incrementally, looking for the smallest scenario change that could make Baseline choose incorrectly while the right context supplied the answer. The broader hypothesis is that context can influence a decision even with a relatively powerful model. These small pilots explore that idea but they do not prove it.

I built a tiny .NET console app with the Microsoft Agent Framework. Its agent can check stock and fulfill orders. The app assembles context before each run; the agent does not search for or fetch context dynamically (like what Posthog do), so this is a simple context-selection experiment rather than a context mill. I used gpt-5-mini for each run, deployed through Microsoft Foundry.

## What changes between runs?

Each scenario gives the agent the customer and stock facts in a prompt. The prompt does not give the shop’s priority rule. The same task is run with three context conditions:

| Condition | Context supplied to the agent |
|---|---|
| **Baseline** | Core instructions; no shop-specific priority rule. |
| **All** | The full context catalog, including unrelated loyalty and pastry notes. |
| **Curated** | Core instructions plus the relevant shortage rule for the scenario. |

“Curated” describes the result: the relevant context is assembled for the task—not the selection method. In this prototype, application code uses manifest tags to choose sources deterministically. A context mill could curate dynamically, as PostHog’s does; this agent does not fetch context at runtime.

The prompt, stock, and tools stay fixed within each scenario. Only the extra context changes.

## Scenario 1 — Ava places her order first, but Ben’s promised pickup is earlier

**Key:** 🤎 = one espresso shot · ⚪ = one oat milk portion

**Shop stock:** 🤎🤎 &nbsp; ⚪

*Two espresso shots and one oat portion: enough for one flat white.*

| Customer | Order | Order placed | Pickup promised |
|---|---|---:|---:|
| Ava | 🤎 + ⚪ | 08:45 | 09:15 |
| Ben | 🤎 + ⚪ | 08:50 | 09:00 |

**Expected:** Ben gets the coffee because his pickup promise is earlier.

**Actual:** ✅ Baseline, All, and Curated all chose Ben.

## Scenario 2 — Ben orders later, but Ava’s promised pickup is earlier

**Key:** 🤎 = one espresso shot · ⚪ = one oat milk portion

**Shop stock:** 🤎🤎 &nbsp; ⚪

*Two espresso shots and one oat portion: enough for one flat white.*

| Customer | Order | Order placed | Pickup promised |
|---|---|---:|---:|
| Ava | 🤎 + ⚪ | 08:45 | 09:00 |
| Ben | 🤎 + ⚪ | 08:50 | 09:15 |

This mirror checks that the result follows the pickup times instead of always favoring Ben.

**Expected:** Ava gets the coffee because her pickup promise is earlier.

**Actual:** ✅ Baseline, All, and Curated all chose Ava.

## Scenario 3 — Pickup times tie, so Ben’s reserved slot takes priority

**Key:** 🤎 = one espresso shot · ⚪ = one oat milk portion

**Shop stock:** 🤎🤎 &nbsp; ⚪

*Two espresso shots and one oat portion: enough for one flat white.*

| Customer | Order | Order placed / queue | Pickup promised |
|---|---|---:|---:|
| Ava | 🤎 + ⚪ | Walk-in, 08:45 | 09:00 |
| Ben | 🤎 + ⚪ | Reserved online, 08:50 | 09:00 |

When pickup times tie, the shop rule gives the reserved slot priority over the walk-in queue. I added this scenario to give Baseline a clear chance to miss a shop-specific rule while All and Curated have it.

**Expected:** Ben gets the coffee because he reserved the pickup slot.

**Actual:** ❌ 😱 Baseline chose Ava; All and Curated chose Ben.

## What the results say

| Scenario | Baseline | All | Curated |
|---|---|---|---|
| 1: Ben has earlier pickup | Ben ✅ | Ben ✅ | Ben ✅ |
| 2: Ava has earlier pickup | Ava ✅ | Ava ✅ | Ava ✅ |
| 3: Reserved-slot tie-break | Ava — ❌ | Ben — ✅ | Ben — ✅ |

Scenario 3 is a promising first signal that the supplied rule changed the choice. Each result is one model call per condition, so this is a small pilot—not evidence that the difference will repeat or that one context strategy is generally better. Let's not _over-extract_ a conclusion from one run (😉).

The offline harness separately checks context assembly, source selection, context size, and stock enforcement. These checks run without a model and say nothing about model quality. The live eval scores observable tool behavior and inventory; it cannot reveal the model’s private reasoning. The scripted demo only exercises the app’s tools.

The order facts belong in the prompt; the shop rule belongs in context. If the model can infer the rule from the facts, adding context may not change its action. That is still a useful result. 

# Next steps

This project does not test dynamic context retrieval, large context windows, or general model improvement. Repeated runs are the next step to see whether Scenario 3’s result holds true.
Id also like to add some more coffee-related puns, but I might start to lose my readership if that continues...
