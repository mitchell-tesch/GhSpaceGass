# ADR-0006: Duplicate Section and Material Names

## Context

Multiple Create Section (or Create Material) components may produce entries with the same name. For library items, duplicates are inherently identical unless they have different optional property overrides (e.g., different AreaFactor). For custom items, same-name entries could have conflicting properties.

Options considered:
- **Deduplicate silently** — Keep first occurrence, no feedback.
- **Deduplicate + warn** — Keep first occurrence, warn about duplicates.
- **Error on conflict** — Error only when same-name custom items have different properties; silently merge identical duplicates.

## Decision

**Deduplicate + warn.** Assemble Model keeps the first occurrence of each unique section/material and emits a GH_RuntimeMessageLevel.Warning listing the duplicates that were dropped.

Uniqueness is determined by the full key: library name + section/material name + all optional property overrides. Two sections with the same library and name but different optional parameters (e.g., different AreaFactor) are treated as **distinct** and both created.

## Consequences

- Simple, consistent rule regardless of whether the duplicate is library or custom.
- The user is always informed when input data is being collapsed, preventing silent surprises.
- Sections with the same library source but different modification factors are correctly preserved as separate entries.
