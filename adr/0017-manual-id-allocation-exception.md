# ADR-0017: Manual ID Allocation Exception for Append Workflows

## Context

The plug-in's established architecture principle (CONTEXT.md) is *"Server-assigned IDs — no manual ID allocation; SpaceGass returns IDs on creation."* Users never type in SpaceGass-internal integer identifiers; they wire domain Goo objects and the assembler resolves them to server IDs.

The default `Clear & Rebuild` workflow (ADR-0001) makes this simple: on every recompute the job is wiped and every ID is freshly assigned by SpaceGass. There is no world in which the user needs to know an existing ID because there are no existing IDs.

The **`Append` mode** amendment to ADR-0001 (Slice 31) broke that assumption. In Append mode the user is deliberately layering new data on top of a `.sg` file that already contains SpaceGass content — content the plug-in did not create and whose IDs it does not know.

For most element types (nodes, members, load cases, restraints, …) this is fine: SpaceGass keeps allocating fresh IDs as new items are pushed, and the plug-in only needs the newly-returned IDs for its own downstream chaining. The user never has to name an existing ID.

Moving-load scenario **combination entries** are the first case where this breaks. Each combination entry produces one or more SpaceGass *combination cases* (regular load cases created behind the scenes by the moving-load engine). The API exposes an optional `StartingCombinationCase` integer that lets the caller pin the starting ID of those generated cases. When appending to a job that already contains combination cases with, say, IDs 1–100, letting SpaceGass auto-assign new starting IDs risks either collisions (rejection at the API) or hard-to-diagnose ID reshuffling.

There is no way for the plug-in to synthesise this integer from Goo — it depends on the state of the existing `.sg` file, which the user is the authority on.

Options considered:

- **Refuse the input** — hold the "no manual ID allocation" line and simply don't expose `StartingCombinationCase`. Users must accept whatever SpaceGass assigns.
- **Provide a resolver Goo** — add a "Combination Case Slot" Goo type whose ID is resolved from a query component. Cleaner in principle but requires the query component to exist and forces every Append-mode user through it.
- **Expose the integer directly** — take an `int` list on the `Create Moving Load Scenario` component (`Starting Combination Cases`, per-entry, `0` = server-assigned). The user is trusted to type a valid, non-colliding starting ID.

## Decision

**Expose the integer directly, as a per-entry list, as a deliberate and documented exception to the Server-assigned IDs principle.** The exception applies only where all three conditions hold:

1. The value refers to an object the plug-in did not create (i.e., pre-existing content in an appended-into job).
2. There is no reasonable Goo abstraction that could stand in for the integer without first querying the live job.
3. The default value (`0` or omit) preserves the original server-assigned behaviour, so the Rebuild-mode user is never asked to think about IDs.

For `StartingCombinationCase` all three conditions hold, so the input is permitted. Future slices that need the same exception (e.g., pinning starting IDs for generated moving-load load cases produced by the Generate endpoint) may reuse this decision without a new ADR.

Element types where the plug-in fully owns the object lifecycle (materials, sections, nodes, members, restraints, plates, load cases, …) remain covered by the original principle: no user-facing integer ID inputs.

## Consequences

- The `Create Moving Load Scenario` component has a `Starting Combination Cases` (`SCC`) integer list input. Per-entry `0` means "let SpaceGass assign".
- The default workflow (Rebuild mode, or Append mode without specifying `SCC`) is unchanged — no user has to know about IDs unless they opt in.
- The append-mode user takes on the responsibility of choosing non-colliding IDs. In the current plug-in there is no companion query component that lists existing combination-case IDs; a future slice should add one so this workflow can be completed inside Grasshopper without opening the `.sg` file externally.
- Any future field that ends up in the same shape (pre-existing content + no Goo abstraction + safe default) may reuse this exception. New fields that do **not** meet all three conditions must still respect the original principle.
