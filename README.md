<p align="center">
  <img src="yak/icon.png" alt="GhSpaceGass" width="128">
</p>

<h1 align="center">GhSpaceGass</h1>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT"></a>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8.0">
  <img src="https://img.shields.io/badge/Rhino-8-000000?logo=rhinoceros" alt="Rhino 8">
  <img src="https://img.shields.io/badge/Grasshopper-8-brightgreen" alt="Grasshopper 8">
  <img src="https://img.shields.io/badge/version-0.2.0-orange" alt="Version 0.2.0">
</p>

A [Grasshopper](https://www.grasshopper3d.com/) plug-in that provides a full round-trip workflow between Grasshopper and [Space Gass](https://www.spacegass.com/) structural analysis via the Space Gass REST API.

Build parametric structural models in Grasshopper, push them to Space Gass, run analysis, and pull results back — all without leaving the canvas. Open existing Space Gass models and inspect their structure, properties, and loads directly on the canvas.

## Features

- **Parametric modelling** — Define members, plates, sections, materials, restraints, releases, offsets, and node constraints with standard Grasshopper data flow.
- **Full load support** — Node loads, member distributed/concentrated/prestress loads, self-weight, lumped mass, prescribed displacements, plate pressure, and thermal loads.
- **Multiple analysis types** — Linear static, non-linear static, buckling, and dynamic frequency analysis with configurable settings.
- **Results extraction** — Node reactions, node displacements, member forces, member displacements, buckling results, dynamic frequency results, and plate forces — all as Grasshopper data trees.
- **Disassemble existing models** — Read structure, properties, load cases, and all load types from an existing Space Gass job back into Grasshopper via query components.
- **Append mode** — Add structure and loads to an existing model without clearing, enabling hybrid workflows between manual Space Gass modelling and Grasshopper.
- **Async execution** — All API-calling components run on background threads. The canvas stays responsive during model assembly, analysis, and results queries.
- **Smart value lists** — Enum inputs auto-populate with value lists on component placement.

## Prerequisites

| Requirement                              | Version |
|------------------------------------------|---|
| [Rhino](https://www.rhino3d.com/)        | 8 |
| [Space Gass](https://www.spacegass.com/) | 14.5+ with REST API (beta) |
| .NET                                     | 8.0 |

## Installation

### From Yak (Rhino Package Manager)

Search for **gh-space-gass** in the Rhino Package Manager and install.

### From Source

```
git clone https://github.com/mitchell-tesch/GhSpaceGass.git
cd GhSpaceGass
dotnet build
```

Copy the built `.gha` and dependencies from `GhSpaceGass/bin/Debug/net8.0/` to your Grasshopper libraries folder.

## Quick Start

1. **Connect** — Drop an `SG Connect` component, set `Connect?` to `true`. The plug-in launches the Space Gass API service automatically. Provide a `.sg` file path to open an existing model, or leave blank for a new temporary job.
2. **Define structure** — Use `SG Section`, `SG Material`, `SG Member`, `SG Plate`, `SG Restraint`, `SG Member Release`, `SG Member Offset`, and `SG Node Constraint` to define your structural model.
3. **Add loads** — Create load cases with `SG Load Case` (and optionally `SG Load Category` and `SG Combination Load Case`). Apply loads with `SG Node Load`, `SG Member Distributed Load`, `SG Member Concentrated Load`, `SG Member Prestress Load`, `SG Self-Weight Load`, `SG Lumped Mass Load`, `SG Prescribed Displacement`, `SG Plate Pressure Load`, and `SG Thermal Load`.
4. **Assemble** — Connect everything into `SG Assemble Model` and set `Assemble?` to `true`. The model is pushed to Space Gass. Use Append mode to add to an existing model without clearing.
5. **Analyse** — Feed the model into `Run Analysis`, select the analysis type, configure optional settings, and set `Run?` to `true`.
6. **Results** — Use results components to extract analysis data back into Grasshopper as structured data trees.
7. **Disassemble** — Use `SG Disassemble Model` to read an existing Space Gass model, then query its properties and loads with the `SG Get` components.

## Component Reference

All components live under the **SpaceGass** tab in Grasshopper.

| Panel | Components |
|---|---|
| **Connection** | SG Connect, SG Job Info, SG Save Job |
| **Properties** | SG Section, SG Material, SG Get Sections, SG Get Materials |
| **Structure** | SG Member, SG Restraint, SG Member Release, SG Member Offset, SG Node Constraint, SG Plate |
| **Cases** | SG Load Case, SG Combination Load Case, SG Load Category, SG Get Load Cases |
| **Loads** | SG Node Load, SG Member Distributed Load, SG Member Concentrated Load, SG Member Prestress Load, SG Self-Weight Load, SG Lumped Mass Load, SG Prescribed Displacement, SG Plate Pressure Load, SG Thermal Load, SG Get Self-Weight Loads, SG Get Node Loads, SG Get Member Loads, SG Get Plate Loads |
| **Model** | SG Assemble Model, SG Disassemble Model |
| **Analysis** | SG Static Analysis Settings, SG Buckling Analysis Settings, SG Dynamic Frequency Settings, Run Analysis |
| **Results** | SG Node Reactions, SG Node Displacements, SG Member Forces, SG Member Displacements, SG Plate Forces, SG Buckling Results, SG Dynamic Frequencies |

## Project Structure

```
GhSpaceGass/          # Grasshopper plug-in — components, icons, async infrastructure
GhSpaceGass.Core/     # Core library — session management, API wrapper, model assembler
GhSpaceGass.Tests/    # Unit tests (xUnit + NSubstitute)
adr/                  # Architecture Decision Records
```

## Architecture

The plug-in follows a **deferred-push** pattern:

- **Builder components** (Create Member, Create Restraint, etc.) construct in-memory data objects without making API calls.
- **Assemble Model** collects all builder outputs, deduplicates geometry, and pushes the complete model to Space Gass via bulk API calls in dependency order. Supports Rebuild (default — clear and push) and Append (add to existing) modes.
- **Disassemble Model** reads an existing model from Space Gass and outputs geometry plus a Model object for downstream chaining.
- **Get components** query specific data (sections, materials, load cases, loads) from the open job.
- **Run Analysis** submits analysis and polls for completion.
- **Results components** query the API and return structured data trees.

A singleton `SpaceGassSession` manages the API service lifecycle. The plug-in launches the service automatically on connect and terminates it on disconnect (if it started it). If the service is already running, it reuses the existing instance.

All async components use a deferred runtime message pattern — messages are queued during background execution and replayed on the UI thread during output, ensuring error badges and warnings display correctly.

Design decisions are recorded in the [`adr/`](adr/) folder.

## License

[MIT](LICENSE) © Mitchell Tesch
