# ADR-0008: Results Output Structure

## Context

Analysis results (displacements, reactions, forces) are returned as lists of records tagged by load case and node/member ID. We need to decide how these are structured as Grasshopper outputs.

Options considered:
- **Flat list** — One list of result objects; user filters manually.
- **Data tree by load case** — Each branch corresponds to a load case, containing element-level results.
- **Data tree by element** — Each branch corresponds to a node/member ID, containing results across all load cases.

## Decision

**Data tree by load case.** Result outputs are organised as GH DataTrees where each branch corresponds to a load case. Within each branch, results are ordered by node/member ID.

## Consequences

- Natural fit for the most common query: "show me all reactions for load case X."
- Consistent across all result types (displacements, reactions, forces).
- Users who need per-element views can flip the tree using native GH components (Flip Matrix, etc.).
- Load case branch indices should be documented or accompanied by a parallel output listing load case IDs/names per branch index.

