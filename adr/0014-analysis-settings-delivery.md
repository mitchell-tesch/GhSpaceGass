# ADR-0014: Analysis Settings Delivery Mechanism

## Context

SpaceGass analysis types (non-linear static, buckling) have numerous configurable settings (e.g., number of buckling modes, convergence criteria, load increments). The Run Analysis component needs access to these settings. We need to decide how settings are delivered.

Options considered:
- **Inline inputs on Run Analysis** — Add settings as optional inputs directly on the Run Analysis component.
- **Builder Goo → Run Analysis** — A separate builder component creates an Analysis Settings Goo that feeds into Run Analysis as an optional input.
- **Async PATCH component** — A separate async component that PATCHes the job's analysis settings via the API, independent of Run Analysis.

## Decision

**Builder Goo → Run Analysis.** A `Create Analysis Settings` builder component produces Analysis Settings Goo. Run Analysis accepts this as an optional input and applies the settings to the job before triggering the analysis run.

## Consequences

- Settings are explicit in the Grasshopper graph — visible and traceable.
- The Run Analysis component remains clean; settings are only connected when non-default behaviour is needed.
- Default analysis runs (no settings connected) work with SpaceGass defaults — zero friction for the common case.
- The builder component can expose a curated subset of the most important settings while SpaceGass defaults handle the rest.
- Settings are applied immediately before each run, ensuring the graph is always the source of truth (consistent with ADR-0001 Clear & Rebuild philosophy).

