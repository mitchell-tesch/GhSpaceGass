# ADR-0003: Accepted Geometry for Member Creation

## Context

SpaceGass members are always straight (node A → node B). The Create Member component needs to decide which Rhino geometry types it accepts and how it maps them to one or more straight members.

Options considered:
- **Line only** — One Line = one member.
- **Line + Polyline** — Polyline segments each become a member sharing intermediate nodes.
- **Any Curve (segmented)** — Arcs, NURBS, etc. discretised into line segments.

## Decision

**Line + Polyline.** The Create Member component accepts `Line`, `LineCurve`, and `Polyline` geometry. A Line produces one member. A Polyline produces N-1 members (one per segment) that share intermediate nodes. Arcs and free-form curves are rejected with an error message directing the user to explode/discretise them first.

## Consequences

- Covers the most common multi-span beam workflow (polylines) without introducing approximation logic.
- The plug-in never silently approximates curved geometry — the user retains full control over discretisation.
- Users working with arcs/curves must pre-process with native GH components (e.g., Divide Curve → Polyline), which is a well-understood pattern.

