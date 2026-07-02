# ADR-0001: Assemble Model Re-run Behaviour

## Context

When the Grasshopper graph recomputes (upstream parameter change), the Assemble Model component fires again. The SpaceGass job already contains the previously pushed model. We need to decide whether to rebuild from scratch or incrementally patch the existing model.

Options considered:
- **Clear & Rebuild** — Close/clear the current job and push the entire model fresh on every recompute.
- **Diff & Patch** — Track previous state, compare with new inputs, and only add/update/delete what changed.

## Decision

**Clear & Rebuild.** Every recompute creates a fresh job and pushes the full model from scratch. The Grasshopper graph is always the single source of truth.

## Consequences

- Simple, predictable behaviour with no stale-state bugs.
- No need to track previous model state or manage ID stability across recomputes.
- Slower on large models — acceptable for the MVP; can be optimised later if profiling shows it's a bottleneck.
- Analysis results are invalidated on every recompute (which is correct — the model changed).

## Amendment (Slice 31)

An opt-in **Append** mode was added to Assemble Model via a Mode input (Rebuild=0, Append=1). When Append is selected, the clear step is skipped and data is pushed alongside existing job content. Clear & Rebuild remains the default. Append mode emits a warning about duplicate risk on recompute. This does not change the original decision — Rebuild is still the recommended workflow; Append exists for the specific use case of augmenting an existing model opened via Connect.

