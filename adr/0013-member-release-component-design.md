# ADR-0013: Member Release Component Design

## Context

SpaceGass members can have releases at each end (Node A and Node B), controlling which degrees of freedom transfer forces through the connection. Each end has 6 DOF (Fx, Fy, Fz, Mx, My, Mz) that can be Fixed, Released, or partially released with a spring stiffness. We need to decide how releases are exposed in the Grasshopper plug-in.

Options considered:
- **Inline on Create Member** — Add 12 boolean inputs (6 per end) directly on the Create Member component.
- **Separate builder component** — A `Create Release` builder component that outputs Release Goo, which then feeds into Create Member as optional inputs (Release A, Release B).
- **Code string inputs** — Two optional string inputs on Create Member (6-char codes like the restraint pattern).

## Decision

**Separate builder component.** A `Create Release` builder component produces Release Goo encapsulating a 6-DOF release code and optional per-DOF stiffness values. The Create Member component gains two optional inputs: `Release A` and `Release B`. When omitted, all DOFs are fixed (fully rigid connection — the SpaceGass default).

## Consequences

- Keeps the Create Member component clean — releases are an advanced feature and don't clutter the basic workflow.
- The same Release Goo can be reused across multiple members (e.g., one pinned release shared by many beam ends).
- Stiffness values are supported from day one, enabling partial/spring releases without a future rework.
- The Create Release component follows the same builder pattern as Create Restraint, maintaining plug-in consistency.
- Two separate Release inputs (A and B) allow different releases at each end, matching the SpaceGass API model.

