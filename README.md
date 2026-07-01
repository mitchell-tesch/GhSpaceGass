<p align="center">
  <img src="yak/icon.png" alt="GhSpaceGass" width="128">
</p>

<h1 align="center">GhSpaceGass</h1>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License: MIT"></a>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet" alt=".NET 8.0">
  <img src="https://img.shields.io/badge/Rhino-8-000000?logo=rhinoceros" alt="Rhino 8">
  <img src="https://img.shields.io/badge/Grasshopper-8-brightgreen" alt="Grasshopper 8">
  <img src="https://img.shields.io/badge/version-0.1.0-orange" alt="Version 0.1.0">
</p>

A [Grasshopper](https://www.grasshopper3d.com/) plug-in that provides a full round-trip workflow between Grasshopper and [Space Gass](https://www.spacegass.com/) structural analysis via the Space Gass REST API.

Build parametric structural models in Grasshopper, push them to Space Gass, run analysis, and pull results back — all without leaving the canvas.

## Features

- **Parametric modelling** — Define members, plates, sections, materials, restraints, releases, offsets, and node constraints with standard Grasshopper data flow.
- **Full load support** — Node loads, member distributed/concentrated/prestress loads, self-weight, lumped mass, prescribed displacements, plate pressure, and thermal loads.
- **Multiple analysis types** — Linear static, non-linear static, buckling, and dynamic frequency analysis with configurable settings.
- **Results extraction** — Node reactions, node displacements, member forces, member displacements, buckling results, dynamic frequency results, and plate forces — all as Grasshopper data trees.
- **Async execution** — All API-calling components run on background threads. The canvas stays responsive during model assembly, analysis, and results queries.
- **Smart value lists** — Auto-complete lists of available values for component input parameters on placement.

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

1. **Connect** — Drop an `SG Connect` component (define the Space Gass install path if not default), set `Connect?` to `true`. The plug-in launches the Space Gass API service automatically.
2. **Define structure** — Use `SG Section`, `SG Material`, `SG Member` (+ accompaning components for Releases and Offsets), `SG Plate` and `SG Constraint` to define your structural model. Add `SG Restraint` components for supports.
3. **Add loads** — Use `SG Load Case` (and `SG Load Category`) and load components (`SG Self-Weight`,`SG Lumped Mass`, `SG Node Load`, `SG Prescribed Displacement`, `SG Member Concentrated Load`, `SG Member Distributed Load`, `SG Member Prestress Load`, `SG Plate Pressure Load` and `SG Thermal Load`) to define loading. Combination load cases can be defined using the `SG Combination Load Case` component.
4. **Assemble** — Connect everything into `SG Assemble Model` and set `Assemble?` to `true`. The model is pushed to Space Gass.
5. **Analysis** — Feed the model into `SG Run Analysis`, define any non-default analysis settings, select the required analysis `Type`, and set `Run?` to `true` to start the solver.
6. **Results** — Use results components (`SG Node Reactions`, `SG Node Displacements`, `SG Member Forces`, `SG Member Displacements`, `SG Plate Forces`, `SG Dynamic Frequencies` and `SG Buckling Results`) to extract analysis results back into Grasshopper.

## Component Reference

All components live under the **SpaceGass** tab in Grasshopper.

| Panel | Components                                                                                                                                                                                                                                                               |
|---|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Connection** | SG Connect, SG Job Info, SG Save Job                                                                                                                                                                                                                                     |
| **Properties** | SG Section, SG Material                                                                                                                                                                                                                                                  |
| **Structure** | SG Member, SG Restraint, SG Member Release, SG Member Offset, SG Node Constraint, SG Plate                                                                                                                                                                               |
| **Loads** | SG Load Case, SG Combination Load Case, SG Load Category, SG Node Load, SG Member Distributed Load, SG Member Concentrated Load, SG Member Prestress Load, SG Self-Weight Load, SG Lumped Mass Load, SG Prescribed Displacement, SG Plate Pressure Load, SG Thermal Load |
| **Model** | SG Assemble Model                                                                                                                                                                                                                                                        |
| **Analysis** | SG Static Analysis Settings, SG Buckling Analysis Settings, SG Dynamic Frequency Settings, SG Run Analysis                                                                                                                                                               |
| **Results** | SG Node Reactions, SG Node Displacements, SG Member Forces, SG Member Displacements, SG Plate Forces, SG Buckling Results, SG Dynamic Frequencies                                                                                                                        |

## Project Structure

```
GhSpaceGass/          # Grasshopper plug-in — components, icons, async infrastructure
GhSpaceGass.Core/     # Core library — session management, API wrapper, model assembler
GhSpaceGass.Tests/    # Unit tests (xunit + NSubstitute)
adr/                  # Architecture Decision Records
```

## Architecture

The plug-in follows a **deferred-push** pattern:

- **Builder components** (Create Member, Create Restraint, etc.) construct in-memory data objects without making API calls.
- **Assemble Model** collects all builder outputs, deduplicates geometry, and pushes the complete model to Space Gass via bulk API calls.
- **Run Analysis** submits analysis and polls for completion.
- **Results components** query the API and return structured data trees.

A singleton `SpaceGassSession` manages the API service lifecycle. The plug-in launches the service automatically on connect and terminates it on disconnect (if it started it). If the service is already running, it reuses the existing instance.

Design decisions are recorded in the [`adr/`](adr/) folder.

## License

[MIT](LICENSE) © Mitchell Tesch


