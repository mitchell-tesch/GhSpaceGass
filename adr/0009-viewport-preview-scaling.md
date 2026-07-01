# ADR-0009: Viewport Preview Scaling for Results

## Context

Results visualisation (reaction arrows, force diagrams, displaced shapes) requires a scale factor to produce meaningful viewport previews. Without scaling, results of vastly different magnitudes would be illegible.

Options considered:
- **User-provided scale input** — Explicit control via a component input.
- **Auto-scale to model extents** — Automatically computed from model bounding box and max result magnitude.
- **Both (auto-scale with override)** — Default to auto-scale; user can override with an explicit input.

## Decision

**Both (auto-scale with override).** By default, the preview scale is computed automatically so that the largest result vector/diagram is a reasonable proportion of the model bounding box. If the user provides a Scale input, it overrides the automatic value.

## Consequences

- Results are always visible out of the box — no manual tuning needed for first use.
- Power users retain full control when they need precise visual sizing.
- The auto-scale logic must be recomputed when results change, adding minor complexity.
- The auto-scale heuristic (e.g., max vector = 10% of bounding box diagonal) should be chosen carefully and may need tuning based on user feedback.

