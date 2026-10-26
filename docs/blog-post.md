---
layout: post
title: "Context Engineering, Served with Oat Milk!"
date: 2026-10-23
categories: [AI, Azure, Foundry, Context Engineering]
---

I like exploring new technology, and I have been trying to make time for small projects that let me spend five or six hours getting hands-on with one idea. Lately, that idea has been context engineering: how the information we give an AI application shapes what it does.

I came across [PostHog's description of its context mill](https://posthog.com/newsletter/context-engineering), which packages maintained knowledge into skills that its agent can choose from at runtime. It made me wonder how much difference context selection could make in a much smaller, more inspectable example.

I chose a subject I care deeply about: oat flat whites. Every software developer has, at some point, dreamed of running a coffee shop. This project let me try that out sitting at home, without the early starts or the milk delivery problem.

## A quick vocabulary primer

An **agent** is an AI application that can use a language model (e.g. an LLM) and tools. The model interprets the request and decides what to do (the 'brain'). Tools let the application interact with the world (the 'limbs and fingers'), such as checking stock or placing an order.

**Context** is the information supplied to the model for a 'turn'. It can include the user's request, previous messages, tool results, instructions, and relevant material from external sources (e.g. via [RAG](link here)). Context engineering is the work of deciding what information to provide the model and how to organize it more efficiently.

An **evaluation**, or eval, checks whether an AI application behaves as expected. It is like a unit test in conventional software, but model behavior can vary between runs, so an eval needs to account for observable behavior and uncertainty.

A **context mill** is one approach to selecting and assembling context for an agent. This is where an agent dynamically asks 'what context do I need for this turn?'. PostHog's system was the inspiration, but this project does not implement a context mill: its selector is deterministic, and it assembles context before the agent runs.

Why care about context engineering? A model can only process a finite amount of context, and relevant information can matter to the decision. In this experiment, the context was *tiny* compared with the model's context window, so the question was not whether we could squeeze it in. It was whether supplying the right policy would change the model's observable choices.

## The experiment: Flat White Panic!

I worked with Codex (after moving back from Claude Code...) to build a small .NET 10 console application using the Microsoft Agent Framework. The agent has tools to inspect and update shop inventory. The app can run a scripted, offline demonstration or, more interestingly, make a live model call using a model configured through Microsoft Foundry.

The first scenario is the Great Oat Milk Shortage:

_"Six customers want oat flat whites, but the shop has only four portions of oat milk."_

The shop needs a policy for choosing which orders to fulfil.

The project grew to include three more scenarios with sharper decision points: an urgent pickup exception, customer-specific substitution approvals alongside an allergy, and a shortage involving espresso as well as milk.

For each scenario I compared three context conditions using the same scenario and tools:

| Condition | What the agent receives |
| --- | --- |
| **Baseline** | Core instructions. Scenario facts, starting inventory, and tool results are still available to the agent. |
| **All** | Every Markdown source in the local context catalog, including material unrelated to the current scenario. |
| **Tag-selected** | Core instructions plus the sources selected by a deterministic manifest and scenario-tag match. This could be dynamically sourced by the model, if we were implementing a 'context mill' like PostHog (a future blog, perhaps). |

The selected pack is assembled by application code before the run and passed as instructions. The agent does not search for, retrieve, or dynamically choose context. 'Tag-selected' is the name used by the experiment; it should not be mistaken for a dynamic context mill.

The hypothesis was that the context supplied to the model would affect its decisions. More specifically, I expected the shortage policy to help the model allocate limited stock correctly, and the selected pack to retain the relevant behavior while using less context than the full catalog.

## Rules the app enforces

Not every requirement is left to the model. The application validates stock and allergy rules when tools are used: inventory cannot go below zero, an allergic customer cannot be served an unsafe drink, and a dairy substitution requires approval from that specific customer. These are deterministic application rules, not evidence that the model independently understood or remembered them.

This distinction was useful while building the experiment. Model behavior is one thing to evaluate; the safety and consistency of the tools are another. I also had deterministic unit tests for those application-level invariants.

## What I measured

The eval has two different jobs, and keeping them separate matters:

1. **Offline harness checks** verify context assembly, source selection, context size, and deterministic tool rules. These run without a model call.
2. **Live behavior checks** inspect observable model tool behavior against scenario expectations, such as fulfilment order, unsafe substitution attempts, and final inventory.

The offline checks confirm that the three context conditions contain the intended sources, that the selected pack is smaller than the full catalog, and that the application rejects invalid tool actions. They do not show that one context makes the model better.

The live runs are small pilots: one model call per condition for each scenario in the latest reports. Here is what those reports show:

| Scenario | Baseline | All | Tag-selected |
| --- | --- | --- | --- |
| Pickup-window exception | Fulfilment order passed ✅ | Fulfilment order passed ✅ | Fulfilment order passed ✅ |
| Scoped substitution approval | Expected allocation failed ❌ | Expected allocation passed ✅ | Expected allocation passed ✅ |
| Three-ingredient bottleneck | Fulfilment order passed ✅ | Fulfilment order passed ✅ | Fulfilment order passed ✅ |

All three conditions passed the observed allergy/substitution safety and nonnegative-inventory checks in these latest runs. The substitution scenario is the interesting wrinkle: All and Tag-selected followed the expected fulfilment order, while Baseline did not. That is one observed result, not a general ranking of the conditions. A single run per condition cannot tell us whether the difference came from context, sampling variability, or some other aspect of the run.

### A closer look: why the substitution order matters

Imagine five customers each order an oat flat white. Here is the setup at a glance:

**Key:** 🤎 = one coffee / espresso shot · ⚪ = one oat milk portion · 🥛 = one dairy milk portion

**Shop stock:** 🤎🤎🤎🤎🤎 &nbsp; ⚪⚪ &nbsp; 🥛🥛  
*Five espresso shots, two oat portions, and two dairy portions.*

| Queue | Customer | Their order | Dairy substitution |
|---|---|---|---|
| Prepaid 1 | Ruby | 🤎 + ⚪ | 🥛 Approved for Ruby |
| Prepaid 2 | Sam | 🤎 + ⚪ | Not approved |
| Walk-in 1 | Taylor | 🤎 + ⚪ | 🚫🥛 Severe dairy allergy |
| Walk-in 2 | Uma | 🤎 + ⚪ | 🥛 Approved for Uma |
| Walk-in 3 | Vic | 🤎 + ⚪ | Not approved |

Each person wants **🤎 + ⚪**. Only two ⚪ oat portions are available, so the shop has to choose carefully where to use them. Ruby and Uma can each safely take 🥛 dairy instead, but their approvals apply only to their own drinks. Taylor must not receive 🥛 dairy.

The shortage policy says to fulfil prepaid orders first, earliest order first, then walk-ins in arrival order. So the expected sequence is Ruby, Sam, Taylor, then Uma. Vic waits because the milk runs out after four drinks.

There is a second decision inside that queue: Ruby and Uma can safely use dairy, while Taylor can only safely use oat. A sensible allocation is therefore:

1. Ruby gets dairy, which she approved as her substitute. She would have had oat but that oat portion goes to someone else.
2. Sam gets oat, since Sam has no approved substitute.
3. Taylor gets oat, because dairy is unsafe for Taylor.
4. Uma gets dairy, which she approved.
5. Vic waits; both milk stocks are used up.

In the latest Baseline run, the agent used oat for Ruby and Sam, then dairy for Uma. It made no unsafe dairy attempt and did not overdraw stock, but Taylor was left waiting. The problem was not the queue alone: the agent spent an oat portion on Ruby even though she had approved dairy. That used the milk Taylor depended on. With All and Tag-selected context, the agent used dairy for Ruby, oat for Sam, oat for Taylor, and dairy for Uma, matching the expected order and leaving Vic waiting.

This is why the scenario is more revealing than simply asking the model to count portions. The tool prevents unsafe orders and negative inventory, but it cannot decide the shop's intended priority or make the best allocation on the model's behalf. Those choices are visible in which valid tool calls the agent makes. In this run, the fuller or relevant policy context coincided with the intended allocation; repeated runs are needed to find out how consistently that happens.

The reports and JSON traces record the context supplied, outputs, tool events, validation, state changes, and outcomes. They do not expose private model reasoning, and the trace should not be read as doing so.

## What I learned

- Context can be an input to agent behavior that we can vary and inspect, even in a small console experiment.
- Evals help make expectations explicit: what should happen, and which invariants must always hold. They describe what the system should do, not the implementation steps it must use.
- It is important to label scripted/offline checks separately from live model behavior. Passing a scripted harness is not evidence of model quality.
- Keeping scenario setup, context assembly, agent execution, and trace reporting distinct made it easier to change the supplied material without rewriting the agent.
- I learned more about connecting Microsoft Agent Framework tools to a small application—and, yes, I managed to break it along the way.

## What this experiment does not show

This is a small learning project, not a controlled benchmark. The live sample is too small to establish that any context condition improves behavior. The context itself is short, so these runs do not demonstrate a context-window or token-cost benefit at realistic scale. They also do not test dynamic retrieval, a context mill, or generalization to other tasks.

## Next steps

The obvious next experiment is repeated, paired live runs with a documented run order and stable model settings. That would help separate a repeatable effect from variation between calls. I would also like to explore dynamic context selection later, while keeping that as a separate step from this deterministic selector.

## Final thoughts

This was a satisfying way to make an abstract idea concrete. I could change the information supplied to the agent, see which sources were selected, and inspect the tool actions that followed. The first lesson is not that one context strategy wins; it is that a useful experiment needs to make its setup and evidence visible—and be honest about what a handful of runs can tell us.

