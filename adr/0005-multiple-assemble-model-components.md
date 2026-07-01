# ADR-0005: Multiple Assemble Model Components

## Context

The plug-in uses a singleton client and SpaceGass supports one open job at a time. If a user places multiple Assemble Model components on the same canvas, they will compete for the same job — the last to execute overwrites the model built by the first.

Options considered:
- **Last writer wins (no guard)** — Silent; user's responsibility.
- **Guard — only one allowed** — Error on the second instance.
- **Allow — but warn** — Both run (last writer wins), with a warning on each instance.

## Decision

**Allow — but warn.** Multiple Assemble Model components are permitted, but each instance emits a GH_RuntimeMessageLevel.Warning when it detects other Assemble Model instances on the canvas. The last one to solve owns the job.

## Consequences

- The user is informed of the conflict without being blocked.
- Supports rare legitimate use cases (e.g., toggling between two model variants via a stream filter upstream of downstream components).
- The warning message should identify how many other Assemble Model instances exist to aid debugging.

