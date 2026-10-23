# Project guidance

## Objective
Build a small educational coffee-shop simulation for learning context engineering, agent evaluation, and observability. Implement only the current first vertical slice: the Great Oat Milk Shortage console experiment comparing baseline, full-catalog, and manifest-driven, tag-selected context.

## Constraints
- .NET 10, C#, Microsoft Agent Framework, xUnit, JSON and Markdown local context.
- No frontend, database, containers, Azure provisioning, vector database, embeddings, or multi-agent orchestration.
- Keep scenario, context assembly, agent execution, and execution trace concerns distinct.
- Context changes must not require changing agent implementation.
- Keep local context sources versioned and make source selection inspectable in traces.
- Distinguish offline harness checks from live model behavior evaluations. Never claim scripted runs demonstrate model improvement.
- Offline scripted mode is supported and labelled distinctly from genuine model runs. Never use its output as evidence of model quality.
- Keep provider settings outside application logic and never hardcode credentials.
- Validate stock and allergy rules in application code; inventory must never be negative.
- Trace observable inputs, outputs, tool events, validation, state, errors, and outcomes. Never claim private model reasoning is observable; never log secrets.
- Preserve experimental integrity: record hypotheses and setup, never invent results.

## Future task instructions
Prefer small readable changes. Explain trade-offs when adding complexity. Consult current official Microsoft Agent Framework guidance when changing provider integration. Maintain `docs/learning-journal.md` and `docs/blog-outline.md` as experiments develop. Do not expand the product vision without the user's direction.
