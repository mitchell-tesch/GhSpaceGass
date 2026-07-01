# ADR-0015: Intermediate Results Tree Structure

## Context

Intermediate member forces and displacements return results at multiple stations along each member, for each load case. The existing results output structure (ADR-0008) uses a single-level data tree branched by load case. Intermediate results introduce a second dimension (member), requiring a decision on tree structure.

Options considered:
- **Single-level branch per load case** — Flat list within each branch containing all stations for all members in sequence. Consistent with ADR-0008 but harder to isolate per-member results.
- **Two-level branch {load case; member}** — Each path has two indices: load case and member. Station results are list items within each branch, ordered by position along the member.
- **Three-level branch {load case; member; station}** — One item per branch. Overly granular; loses the natural "list of stations" grouping.

## Decision

**Two-level branch `{load_case_index; member_index}`.** Intermediate results use a two-level data tree path where the first index is the load case and the second index is the member. Within each branch, station results are ordered by position along the member (start → end).

## Consequences

- Natural grouping: extracting all stations for one member in one load case is a single branch access.
- Consistent with ADR-0008's load-case-first philosophy while accommodating the additional member dimension.
- Users can use native GH tree manipulation (Flip Matrix, Tree Branch, etc.) to reorganise by member-first if needed.
- The `Get Member Forces` and `Get Member Displacements` components output a parallel `Members` (Line geometry) list and `Load Cases` (string) list to map branch indices back to domain objects.
- Both End Forces and Intermediate modes on `Get Member Forces` use the same two-level `{load_case; member}` tree structure for consistency. End Forces branches contain 2 items (Node A, Node B); Intermediate branches contain N items (one per station).

