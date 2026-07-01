# ADR-0002: Orphan Geometry Handling

## Context

A restraint or node load may reference a Point3d that does not coincide (within tolerance) with any member endpoint. We need to decide how Assemble Model handles these "orphan" points.

Options considered:
- **Create a standalone node (silent)** — Permissive; creates the node without informing the user.
- **Error / warning and skip** — Strict; blocks the restraint/load from being created.
- **Warning + create** — Creates the standalone node but emits a Grasshopper warning so the user is aware.

## Decision

**Warning + create.** Assemble Model creates a standalone node in SpaceGass for any restraint/load point that doesn't match a member endpoint, and emits a GH_RuntimeMessageLevel.Warning on the component identifying the orphan point(s).

## Consequences

- The workflow is never blocked by accidental geometric misalignment.
- The user is explicitly informed of disconnected nodes, making debugging easy.
- SpaceGass may report its own warnings about unconnected nodes during analysis, providing a second signal.

