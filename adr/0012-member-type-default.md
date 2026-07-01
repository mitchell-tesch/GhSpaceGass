# ADR-0012: Member Type Default

## Context

SpaceGass members have a type (beam, truss, cable, compression-only, tension-only, gap, fuse). The Create Member component needs a default value.

Options considered:
- **Default to beam** — Most common type. Optional input to override.
- **Required input** — User must always specify the member type.

## Decision

**Default to beam.** The Create Member component defaults to beam type. An optional Type input allows the user to specify alternatives (truss, cable, etc.).

## Consequences

- Simple portal frames and similar structures require no type input — minimal friction.
- Truss and cable analyses require the user to explicitly set the type, which is appropriate since these change the structural behaviour fundamentally.
- The optional input should accept the SpaceGass `MemberType` enum values, exposed as a dropdown or value list.

