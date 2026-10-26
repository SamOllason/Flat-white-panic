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

**Context** is the information supplied to the model for a 'turn'. It can include the user's request, previous messages, tool results, instructions, and relevant material from external sources (e.g. via retrieval-augmented generation, or RAG). Context engineering is the work of deciding what information to provide the model and how to organize it more efficiently.

An **evaluation**, or eval, checks whether an AI application behaves as expected. It is like a unit test in conventional software, but model behavior can vary between runs, so an eval needs to account for observable behavior and uncertainty.

A **context mill** is one approach to selecting and assembling context for an agent. This is where an agent dynamically asks 'what context do I need for this turn?'. PostHog's system was the inspiration, but this project does not implement a context mill: its selector is deterministic, and it assembles context before the agent runs.

Why care about context engineering? A model can only process a finite amount of context, and relevant information can matter to the decision. In this experiment, the context was *tiny* compared with the model's context window, so the question was not whether we could squeeze it in. It was whether supplying the right policy would change the model's observable choices.

## The experiment: Flat White Panic!

I worked with Codex (after moving back from Claude Code...) to build a small .NET 10 console application using the Microsoft Agent Framework. The agent has tools to inspect and update shop inventory. The app can run a scripted, offline demonstration or, more interestingly, make a live model call using a model configured through Microsoft Foundry.

I reduced the main scenario to two customers and one oat portion. Ava arrives first as a walk-in. A few minutes later, Ben places a prepaid mobile order. Both want an oat flat white, but only one can be made. Who should get it?

The request gives the agent the facts—who ordered, when, and what is in stock—but does not state the shop's tie-break rule. That rule lives in a context file: when stock is short, prepaid orders go first. With the rule, Ben gets the coffee; without it, the agent has to guess.

I compare three context conditions using the same scenario and tools:

| Condition | What the agent receives |
| --- | --- |
| **Baseline** | Core instructions. Scenario facts, starting inventory, and tool results are still available, but the prepaid-first shop policy is not supplied. |
| **All** | Every Markdown source in the local context catalog, including material unrelated to the current scenario. |
| **Tag-selected** | Core instructions plus the shortage policy selected by a deterministic manifest and scenario-tag match. |

The selected pack is assembled by application code before the run and passed as instructions. The agent does not search for, retrieve, or dynamically choose context. 'Tag-selected' is the name used by the experiment; it should not be mistaken for a dynamic context mill.

The hypothesis is deliberately narrow: supplying the prepaid-first rule should make Ben more likely to receive the last coffee. Tag-selected should include that rule without needing to send unrelated catalog notes. If Baseline also chooses Ben, that is still useful: the model may infer the expected policy from the facts, so this particular example would not show a behavioral difference.

## Rules the app enforces

Not every requirement is left to the model. The application validates stock and allergy rules when tools are used: inventory cannot go below zero, an allergic customer cannot be served an unsafe drink, and a dairy substitution requires approval from that specific customer. These are deterministic application rules, not evidence that the model independently understood or remembered them.

This distinction was useful while building the experiment. Model behavior is one thing to evaluate; the safety and consistency of the tools are another. I also had deterministic unit tests for those application-level invariants.

## What I measured

The eval has two different jobs, and keeping them separate matters:

1. **Offline harness checks** verify context assembly, source selection, context size, and deterministic tool rules. These run without a model call.
2. **Live behavior checks** inspect observable model tool behavior against scenario expectations, such as which order gets the last portion and whether inventory remains safe.

The offline checks confirm that the three context conditions contain the intended sources, that the selected pack is smaller than the full catalog, and that the application rejects invalid tool actions. They do not show that one context makes the model better.

The live runs are small pilots: one model call per context condition for this scenario. The eval checks whether Ben gets the last oat portion and whether inventory stays nonnegative. The separate offline harness checks the allergy and substitution guardrails. It scores behavior we can observe through tool calls; it cannot tell us the model's private reasoning.

**Latest live result:** pending the first run of this simplified scenario.

| Scenario | Baseline | All | Tag-selected |
| --- | --- | --- | --- |
| One oat portion: Ava is first in line; Ben is prepaid | Pending | Pending | Pending |

### The coffee queue, at a glance

**Key:** 🤎 = one espresso shot · ⚪ = one oat milk portion

**Shop stock:** 🤎🤎 &nbsp; ⚪

*Two espresso shots and one oat portion: enough for one flat white.*

| Customer | Order | Fact the agent receives |
|---|---|---|
| Ava | 🤎 + ⚪ | Walk-in; arrived at 08:50 |
| Ben | 🤎 + ⚪ | Prepaid mobile order; placed at 08:53 |

The expected choice comes from a shop rule, not the timestamps: **prepaid orders take priority over walk-ins during a shortage.** So Ben gets the one oat flat white, and Ava waits. The timestamps and stock are facts in the request; the priority rule is the context we vary.

That distinction is the learning point. Baseline is not missing the order details—it is missing the shop's policy. All and Tag-selected receive that policy, but All also receives unrelated catalog material. The comparison asks whether the specific rule changes the agent's observable choice, and whether tag selection can provide that rule without the extra material.

One run per condition is only a first look. If the conditions differ, repeat runs are needed to see whether the difference is consistent. If all conditions make the same choice, we have not demonstrated an effect in this scenario; we should report that plainly rather than redesign the scoring to force a failure.

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

The next experiment is repeated, paired live runs with stable model settings, to see whether the one-rule result repeats. Later, I can bring back richer cases—pickup exceptions, customer-specific substitutions, allergies, and multiple ingredient shortages—as follow-up experiments once this basic comparison is clear. Dynamic context selection remains a separate future step.

## Final thoughts

This was a satisfying way to make an abstract idea concrete. I could change the information supplied to the agent, see which sources were selected, and inspect the tool actions that followed. The first lesson is not that one context strategy wins; it is that a useful experiment needs to make its setup and evidence visible—and be honest about what a handful of runs can tell us.

