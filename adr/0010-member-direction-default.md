# ADR-0010: Member Direction / Orientation Default

## Context

SpaceGass members have local axes controlled by a direction reference (vector or angle). This affects local-axis load interpretation and result diagram orientation. We need to decide whether the Create Member component exposes this.

Options considered:
- **Omit — use SpaceGass defaults** — No direction input; SpaceGass assigns automatic orientation.
- **Optional input** — An optional Direction input (Vector3d or angle) that overrides the default when provided.

## Decision

**Optional input.** The Create Member component includes an optional Direction input. When omitted, SpaceGass automatic orientation applies. When provided, it sets the member's reference direction via the API's `DirectionUpdate`.

## Consequences

- Users working with simple frames can ignore the input entirely — zero friction.
- Users with inclined beams, channels, or asymmetric sections can control orientation without a workaround.
- The Goo type (`SgMember`) must carry an optional direction field, and Assemble Model must map it to the API's `DirectionUpdate` when present.

