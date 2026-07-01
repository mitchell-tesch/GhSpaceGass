# ADR-0004: Connect and Existing Service

## Context

When the Connect component runs, the SpaceGass API service may already be listening on the target port — from a previous GH session, a manual start, or a crash recovery. We need to decide how Connect handles this.

Options considered:
- **Reuse if running** — Probe the port; if responsive, connect directly without launching a new process.
- **Always kill & restart** — Terminate any existing instance and launch fresh.
- **Fail if occupied** — Error out and require the user to resolve the conflict.

## Decision

**Reuse if running.** Connect probes the service endpoint first. If the API is already responsive, it connects to the existing instance. If not, it launches `SpaceGassApi.exe`. The Clear & Rebuild strategy in Assemble Model (ADR-0001) already guarantees clean model state regardless of prior service state.

## Consequences

- Fast reconnect after GH crashes or canvas reloads — no orphan-process management needed.
- User can pre-start the service manually if desired.
- If a non-SpaceGass process occupies the port, the health check will fail and the component should report a clear error suggesting a different port.

