# ADR-0007: Service Cleanup on Disconnect

## Context

When Grasshopper closes, the Connect component is removed, or the user toggles `Connect?` to false, the plug-in needs to decide what happens to the SpaceGass API service process it launched.

Options considered:
- **Kill the service** — Terminate the process on GH unload if the plug-in started it.
- **Leave it running** — Keep the service alive for post-session inspection or quick reconnection.
- **Close job, leave service** — Free the file lock but keep the process running.

## Decision

**Kill the service.** If the plug-in launched the SpaceGass API service process, it terminates it when:
- Grasshopper unloads (via `GH_AssemblyPriority` lifecycle hook)
- The Connect component is removed from the canvas
- The user sets the Connect component's `Connect?` input to false (explicit disconnect)

If the plug-in connected to a pre-existing service (ADR-0004), it does not terminate it in any of these cases.

## Consequences

- No orphan `SpaceGassApi.exe` processes accumulate across GH sessions.
- The user can explicitly disconnect and reconnect by toggling `Connect?`, which disposes and recreates the session each time.
- The user cannot inspect the last model in SpaceGass after disconnecting — but they can provide a file path to persist the job.
- The distinction between "we launched it" vs "we found it running" must be tracked by the singleton client.
