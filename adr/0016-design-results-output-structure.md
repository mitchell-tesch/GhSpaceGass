# ADR-0016: Design Results Output Structure

## Context

Analysis results (reactions, displacements, forces) are tagged per element per load case, and ADR-0008 established the convention that they are output as data trees branched by load case. Steel design results, however, are structurally different: the design engine iterates over all load cases and load segments internally and produces a **single aggregated record per element** reporting the *critical* load case. The critical load case is a value on each record, not a dimension of the result set.

We need to decide how design results are exposed in Grasshopper without contradicting ADR-0008 or forcing an unnatural structure.

Options considered:
- **Data tree by load case** — Same as analysis results (ADR-0008). Would require creating a branch for every load case that appears as any element's critical case, with each branch containing only the elements governed by that case. Confusing because the tree looks like a "per-load-case" query but the underlying data is per-element.
- **Data tree by element** — Each branch corresponds to a member/plate ID with one item (the critical summary). Overly granular for a per-element result set — one branch per member is just a flat list dressed up as a tree.
- **Flat lists with critical case as a value column** — One parallel list per output field, indexed by member ID. Critical case ID and name appear as regular outputs (`CC`, `CCN`).

## Decision

**Flat lists.** Design result components (starting with `Get Steel Member Check Summary`) output parallel flat lists, one entry per queried element ordered by element ID. The governing load case is exposed as a value on each record — an integer `Critical Cases` output plus a resolved-name `Critical Case Names` output — not as a data-tree dimension.

This is a deliberate deviation from ADR-0008, which applies to *analysis* results where the load case is a genuine dimension (a result value per element per load case). Design results are post-processing outputs that have already collapsed the load-case dimension by design.

## Consequences

- Simpler, more honest data shape for the user: "here is a table of steel checks, one row per member."
- Users who want to group members by their critical load case can use native GH grouping (`Sort List`, `Group`, `Member Index`) with the parallel `Critical Cases` list.
- No misleading "empty branches" if some load cases never govern any member.
- Establishes a clear pattern for future design components (e.g., steel connection design, concrete design) — they should also use flat lists unless they genuinely have multiple records per element.
- If a future design query legitimately produces multiple records per element (e.g., detailed per-segment checks rather than the aggregated summary), a two-level `{element; record}` tree may be introduced as an additional pattern; this ADR does not preclude that.
