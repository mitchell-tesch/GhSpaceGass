# ADR-0011: Load Axes Default

## Context

SpaceGass member loads can be applied in global or local member axes. The Create Member Distributed Load component needs a default axis system. Node loads are always global (ADR defined at creation).

Options considered:
- **Default to global** — Matches how most users think about gravity and wind loads. Optional input to switch to local.
- **Default to local** — Local axes are the natural reference frame for member-specific actions (e.g., loads perpendicular to the member).
- **Required input** — No default; user must always specify.

## Decision

**Default to local for member loads.** Member distributed loads default to local member axes. An optional Axes input allows the user to switch to global axes when needed (e.g., gravity loads applied in the global Y direction). Node loads are always global — no axes input needed on that component.

## Consequences

- Loads applied relative to the member (e.g., perpendicular UDL on an inclined beam) work correctly without extra inputs — the most common member-specific use case has zero friction.
- Gravity loads require the user to explicitly set Axes to Global, but this is a conscious configuration choice that is clear on the canvas.
- Node loads are always global (no axes input needed on that component).
